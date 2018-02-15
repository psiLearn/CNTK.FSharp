open System.Xml
open System.Text
#I "../../packages/FSharp.Data/lib/net45"
#I "../../packages/SharpZipLib/lib/20"
#r "FSharp.Data.dll"
#r "ICSharpCode.SharpZipLib.dll"
#r "System.Xml.Linq.dll"

[<AutoOpen>]
module cifarUtil = 
    open System.IO
    open System.Xml.Linq
    open System.Drawing
    open System.Runtime.InteropServices
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

    /// reading the labels from "batches.meta.txt"
    let getLabelNames src =
        File.ReadAllLines <| src @@ "batches.meta.txt"

    /// load the data from the bin files
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

    /// converts to a bitmap
    let toBitmap (width, height) (im:byte [,,]) = 
        let bArray = Array.zeroCreate (3*width*height)
        bArray
        |> Array.iteri (fun i _ ->
            let x = i/(3*width)
            let y = ( i%(3*width) ) / 3
            let c = i%3
            bArray.[i] <- byte im.[x,y,c])
        let bmp = new Bitmap(width, height, Imaging.PixelFormat.Format24bppRgb)        
        let bmpData = 
            bmp.LockBits(   Rectangle(0, 0,bmp.Width, bmp.Height),
                            Imaging.ImageLockMode.WriteOnly,
                            bmp.PixelFormat);
        let pNative = bmpData.Scan0;
        Marshal.Copy(bArray, 0, pNative, bArray.Length);
        bmp.UnlockBits(bmpData);
        bmp
        
    /// saving a byte[3,iDm,height] array as a png 
    let saveAsPNG (width, height) fName (im:byte [,,])  = 
        use bmp = toBitmap (width, height) (im:byte [,,]) 
        bmp.Save(fName);
    
    /// function to IO a byteArray:
    /// (width,height) foldername fileNumber byte[,,]
    type SaveFunction =  (int*int) -> string -> int -> byte[,,] -> unit
    
    /// save a image
    let saveImageFunction (width,height) foldername iFile imData= 
        let fName = foldername @@ sprintf "%05d.png" iFile 
        printfn "saving %s" fName
        saveAsPNG (width,height) fName imData
           
    let  saveImages (fo:SaveFunction option) (width,height)  (data: (byte * byte[]) list) foldername =
        if  Directory.Exists(foldername) |> not then 
            Directory.CreateDirectory foldername |> ignore
        data 
        |> List.indexed
        |> List.fold (fun (acc:int[,,]) (iFile,(_label, im)) ->
            let imData = Array3D.zeroCreate width height 3
            im 
            |> Array.splitInto (3)                      // colors
            |> Array.iteri (fun c ca -> 
                ca |> Array.chunkBySize (width)
                |> Array.iteri (fun x cv -> 
                    cv |> Array.iteri (fun y b -> 
                        imData.[x,y,c] <-   b
                        acc.[x,y,c] <- acc.[x,y,c] + (int b))
            ))
            fo |> Option.iter (fun f ->
                f (width,height) foldername iFile imData)
            acc
        )  (Array3D.zeroCreate width height 3)
        |> Array3D.map (fun  v -> v / data.Length) 
    
    /// get the labels from the loaded data
    let getLabels:(byte * byte[])list -> (int * byte) list  = List.map fst >> List.indexed 
    
    /// computes the mean values
    let letGetMeanAndSave (width,height) folder  data=     
        let dirPath =  __SOURCE_DIRECTORY__ @@ folder
        let saveFunctionOption = 
            match System.IO.Directory.Exists <| dirPath with
            | false -> Some saveImageFunction
            | true -> None
        saveImages saveFunctionOption (width, height) data dirPath

    let xn = XName.Get 
    let saveMeanXml (width:int,height:int) (fName:string) data =
        let xDoc = XDocument()
        let stringStream = new StringWriter()
        data
        |> Array3D.iteri ( fun i j k v ->
                            let sFun = match i with 
                                        | 0 -> sprintf " %e" 
                                        | _ -> sprintf " %e\n" 
                            let s = float v |> sFun                 
                            s |> stringStream.Write  )

        let datastr = stringStream.ToString();
        let r = XElement(xn "opencv_storage",                    
                    [   XElement(xn "Channel",3) 
                        XElement(xn "Row",width) 
                        XElement(xn "Col",height) 
                        XElement(xn "MeanImg",
                            XAttribute (xn "type_id", "opencv-matrix"),
                            [
                                XElement(xn "rows", 1)
                                XElement(xn "cols",width*height*3)
                                XElement(xn "dt", "f")
                                XElement(xn "data", datastr)
                            ])
                    ]
            )
        xDoc.Add  r
        let settings = XmlWriterSettings()
        settings.Encoding <-  ASCIIEncoding();
        use writer = XmlWriter.Create( fName, settings )
        xDoc.Save( writer )
        
    let saveTxt filename (data:(byte * byte[]) list) = 
        data
        |> List.toArray
        |> Array.Parallel.map (fun (l,d)-> 
            d 
            |> Array.fold (sprintf "%s %d") ""
            |> sprintf "|labels %d |features %s" l )

        |> fun lines -> File.WriteAllLines(filename, lines)
        
//////////////////////////////////////////////

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
// added some guard to protekt from long taking download
if  __SOURCE_DIRECTORY__ @@ "test" 
    |> System.IO.Directory.Exists
    |> not then
                // the actual download
                download dataSetUrl dataFileName
                // get the data from the tar archive
                let tar = decompress dataFileName
                untar tar |> ignore
                // deleting old data                
                System.IO.File.Delete dataFileName
                System.IO.File.Delete tar
                

// get the data  into memory
let (trn,test,LabelNames) = 
    __SOURCE_DIRECTORY__ @@ @"cifar-10-binary\cifar-10-batches-bin"
    |> loadData [nColors; width; height]

// only saving images if there is no train directory
let dataMeanTraining =     
    letGetMeanAndSave (width,height) "train" trn
let dataMeanTest =     
    letGetMeanAndSave (width,height) "test" test

// saving the trainign data to file
let saveTExtWithGuard fn data = 
    if System.IO.File.Exists fn |> not then 
        saveTxt fn data 
saveTExtWithGuard (__SOURCE_DIRECTORY__ @@ "Train_cntk_text.txt") trn
saveTExtWithGuard (__SOURCE_DIRECTORY__ @@ "Test_cntk_text.txt") test 

dataMeanTraining |> saveMeanXml (width, height) (__SOURCE_DIRECTORY__ @@ "CIFAR-10_mean.xml")

// here some functions that i would preferred to use,
// but that would need some CNTK custom reader

// getting the labels of the training data
let writeMap dir mapName data=
    let foldername = __SOURCE_DIRECTORY__ @@ dir
    data 
    |> getLabels
    |> List.map (fun (i,l) -> 
        sprintf "%s\t%d" (foldername @@ sprintf "%05d.png" i) l
        ) 
    |> fun lines -> System.IO.File.WriteAllLines(__SOURCE_DIRECTORY__ @@ mapName,lines)

writeMap "train"  "train_map.txt" trn
writeMap "test"  "test_map.txt" test

do 
    trn
    |> List.map (fun (l,d) ->
        d 
        |> Array.splitInto 3
        |> Array.map (Array.map float >> Array.average >> (fun v -> v / 255.))
        |> fun a -> sprintf "|regrLabels\t%f\t%f\t%f" a.[0] a.[1] a.[2])
    |> fun lines -> System.IO.File.WriteAllLines(__SOURCE_DIRECTORY__ @@ "train_regrLabels.txt", lines)  

let writeRegrLabels fName data =  
    data
    |> List.map (fun (l,d) ->
        d 
        |> Array.splitInto 3
        |> Array.map (Array.map float >> Array.average >> (fun v -> v / 255.))
        |> fun a -> sprintf "|regrLabels\t%f\t%f\t%f" a.[0] a.[1] a.[2])
    |> fun lines -> System.IO.File.WriteAllLines(__SOURCE_DIRECTORY__ @@ fName, lines)  
 
writeRegrLabels "train_regrLabels.txt" trn
writeRegrLabels "test_regrLabels.txt" test
        

// mean as png
let saveMeanPng fName = Array3D.map byte >> saveAsPNG (width,height) (__SOURCE_DIRECTORY__ @@ fName)
dataMeanTest |> saveMeanPng "test/mean.png"
dataMeanTraining |> saveMeanPng "train/mean.png"

