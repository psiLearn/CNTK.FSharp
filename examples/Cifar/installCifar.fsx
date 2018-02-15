open System.Runtime.InteropServices
#I "../../packages/FSharp.Data/lib/net45"
#I "../../packages/SharpZipLib/lib/20"
#r "FSharp.Data.dll"
#r "ICSharpCode.SharpZipLib.dll"


[<AutoOpen>]
module cifarUtil = 
    open System.IO
    open System.Drawing
    open System.IO.Compression
    open FSharp.Data
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

    /// Reads the information of a 
    let readBatch dims src = 
        use fs = new FileStream(src,FileMode.Open)
        use reader = new BinaryReader(fs)
        let rec readPictures pictures = 
            if fs.Position < fs.Length then 
                let feature = reader.ReadByte()
                let picture = reader.ReadBytes(dims |> List.reduce (*))
                (feature,picture)::pictures |> readPictures 
            else pictures   
        readPictures []                 

    let getLabelNames src =
        File.ReadAllLines <| src @@ "batches.meta.txt"

    let loadData dims src =
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
                readBatch dims f |> List.append acc
                 )  []    
        foldGroup groups.[0], foldGroup groups.[1], getLabelNames src

        
    let saveAsPNG fName (im:byte [,,]) (iDim, jDim) = 
        let bArray = Array.zeroCreate (iDim*jDim*3)
        bArray
        |> Array.iteri (fun i _ ->
            let x = i/(3*iDim)
            let y = ( i%(3*iDim) ) / 3
            let c = i%3
            bArray.[i] <- byte im.[c,x,y])
        use bmp = new Bitmap(iDim, jDim, Imaging.PixelFormat.Format24bppRgb)        
        let bmpData = 
            bmp.LockBits(   Rectangle(0, 0,bmp.Width, bmp.Height),
                            Imaging.ImageLockMode.WriteOnly,
                            bmp.PixelFormat);

        let pNative = bmpData.Scan0;
        Marshal.Copy(bArray, 0, pNative, bArray.Length);

        bmp.UnlockBits(bmpData);

        bmp.Save(fName);
    
    type SaveFunction =  (int*int) -> string -> int -> byte[,,] -> unit
    let saveImageFunction (iDim,jDim) foldername iFile imData= 
        let fName = Path.Combine (foldername, sprintf "%05d.png" iFile )
        printfn "saving %s" fName
        saveAsPNG fName imData (iDim,jDim)
           

    let  saveTrainImages (fo:SaveFunction option) (iDim,jDim)  (data: (byte * byte[]) list) foldername =
        if  Directory.Exists(foldername) |> not then 
            Directory.CreateDirectory foldername |> ignore
        data 
        |> List.indexed
        |> List.fold (fun (acc:int[,,]) (iFile,(_label, im)) ->
            let imData = Array3D.zeroCreate 3 iDim jDim
            im 
            |> Array.splitInto (3)
            |> Array.iteri (fun i ca -> 
                ca |> Array.chunkBySize (iDim)
                |> Array.iteri (fun j cv -> 
                    cv |> Array.iteri (fun k b -> 
                        imData.[i,j,k] <-   b
                        acc.[i,j,k] <- acc.[i,j,k] + (int b))
            ))
            fo |> Option.iter (fun f ->
                f (iDim,jDim) foldername iFile imData)
            acc
        )   (Array3D.init 3 iDim jDim (fun _ _ _ -> 0))
        |> Array3D.map (fun  v -> v / data.Length) 
    let getLabels:('a * 'b)list -> (int * 'a) list  = List.map fst >> List.indexed 
    let letGetMeanAndSave (width,height) folder  data=     
        let dirPath =  __SOURCE_DIRECTORY__ @@ folder
        let saveFunctionOption = 
            match System.IO.Directory.Exists <| dirPath with
            | false -> Some saveImageFunction
            | true -> None
        saveTrainImages saveFunctionOption (width, height) data dirPath

/// URL of the binary dataset
let dataSetUrl = "http://www.cs.toronto.edu/~kriz/cifar-10-binary.tar.gz"

/// File name of the downloaded data
let dataFileName = __SOURCE_DIRECTORY__ + "/cifar-10-binary.tar.gz"

// Dimensions of the data:
//      Number of colors, width of the picture, height of the picture
let nColors = 3;
let width = 32;
let height = 32;

// Downloading the data
if  __SOURCE_DIRECTORY__ @@ "test" 
    |> System.IO.Directory.Exists
    |> not then
                download dataSetUrl dataFileName
// get the data from the tar archive
                decompress dataFileName
                |> untar 
                |> ignore

// get the data  into memory
let (trn,test,LabelNames) = 
    @"C:\Users\siebkpte\fsharp\Cntk\CNTK.FSharp\examples\Cifar\cifar-10-binary\cifar-10-batches-bin"
    |> loadData [nColors; width; height]

// getting the labels of the training data
let trnLabels = trn |> getLabels
let testLabels = test |> getLabels

// save the images and get the mean data

// only saving if there is no train directory
let dataMeanTraining =     
    letGetMeanAndSave (width,height) "train" trn
let dataMeanTest =     
    letGetMeanAndSave (width,height) "test" test

dataMeanTest.[2,0,0]