#I "../../packages/FSharp.Data/lib/net45"
#I "../../packages/SharpZipLib/lib/20"
#r "FSharp.Data.dll"
#r "ICSharpCode.SharpZipLib.dll"


[<AutoOpen>]
module cifarUtil = 
    open FSharp.Data
    open System.IO
    open System.IO.Compression
    open ICSharpCode.SharpZipLib.Tar

    /// Combine paths
    let (@@) a b = Path.Combine(a,b)

    /// Download the file from the given URL
    let download url path = 
        let request = Http.RequestStream(url) 
        use outputFile = new System.IO.FileStream(path,System.IO.FileMode.Create) 
        request.ResponseStream.CopyTo( outputFile )
    
    /// decompresses the file and gives back the new filename
    let decompress fileName =
        let fi = FileInfo fileName
        use originalFileStream = fi.OpenRead()
        let currentFileName = fi.FullName;
        let newFileName = currentFileName.Remove(currentFileName.Length - fi.Extension.Length)
        use decompressedFileStream = File.Create(newFileName)
        use decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress)
        decompressionStream.CopyTo(decompressedFileStream)
        decompressedFileStream.Close()
        newFileName

    /// untars the data in (new) directory, and gives back the directory name 
    let untar fileName = 
        let fi = FileInfo fileName
        let dirName = fileName.Remove(fileName.Length - fi.Extension.Length)
        if Directory.Exists dirName |> not then 
            Directory.CreateDirectory dirName |> ignore
        use fs = new FileStream(fileName,FileMode.Open)
        use tar = TarArchive.CreateInputTarArchive(fs )
        tar.ExtractContents dirName
        dirName
 
    let readBatch src = 
        use fs = new FileStream(src,FileMode.Open)
        use reader = new BinaryReader(fs)
        let rec readPictures pictures = 
            if fs.Position < fs.Length then 
                let feature = reader.ReadByte()
                let picture = reader.ReadBytes(32*32*3)
                (feature,picture)::pictures |> readPictures 
            else pictures   
        readPictures []                 

    let loadData src =
        let files = Directory.GetFiles(src,"*.bin")
        let groups = 
            files
            |> Array.groupBy (fun n -> 
                let c = 
                    (Path.GetFileNameWithoutExtension n)
                    |> Seq.last
                c >= '0' && c <= '9'        )
            |> Array.map snd
        let foldGroup gr= 
            gr 
            |> Array.fold (fun acc f-> 
                readBatch f |> List.append acc
                 )  []    
        foldGroup groups.[0], foldGroup groups.[1]


    let  saveTrainImages (iDim,jDim)  (data: (byte * byte[]) list) foldername =
        if  Directory.Exists(foldername) |> not then 
            Directory.CreateDirectory foldername |> ignore
        data 
        |> List.indexed
        |> List.fold (fun (acc:int[,,]) (iFile,(_label, im)) ->
            let fName = Path.Combine (foldername, sprintf "%05d.png" iFile )
            printfn "not saving %s" fName
            //todo ad save png here
            im 
            |> Array.splitInto (3)
            |> Array.iteri (fun i ca -> 
                ca |> Array.chunkBySize (iDim)
                |> Array.iteri (fun j cv -> 
                    cv |> Array.iteri (fun k b -> 
                        acc.[i,j,k] <- acc.[i,j,k] + (int b))
                    )
            )
            acc
        )   (Array3D.init 3 iDim jDim (fun _ _ _ -> 0))
        |> Array3D.map (fun  v -> v / data.Length) 

/// URL of the binary dataset
let dataSetUrl = "http://www.cs.toronto.edu/~kriz/cifar-10-binary.tar.gz"

/// File name of the downloaded data
let dataFileName = __SOURCE_DIRECTORY__ + "/cifar-10-binary.tar.gz"

download dataSetUrl dataFileName
decompress dataFileName
|> untar 
let (trn,test) = 
    @"C:\Users\siebkpte\fsharp\Cntk\CNTK.FSharp\examples\Cifar\cifar-10-binary\cifar-10-batches-bin"
    |> loadData

// 
//     @"C:\Users\siebkpte\fsharp\Cntk\CNTK.FSharp\examples\Cifar\cifar-10-binary\cifar-10-batches-bin\data_batch_1.bin"
//     |> readBatch
let dataMeanTest =     
    saveTrainImages (32, 32) test (__SOURCE_DIRECTORY__ @@ "Test")

dataMeanTest.[2,0,0]