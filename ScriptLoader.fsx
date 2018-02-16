(*
This file is intended to load dependencies in an F# script,
to train a model from the scripting environment.
CNTK, CPU only, is assumed to have been installed via Paket.
*)

open System
open System.IO

Environment.SetEnvironmentVariable("Path",
    Environment.GetEnvironmentVariable("Path") + ";" + __SOURCE_DIRECTORY__)

let dependencies = [
        "./packages/CNTK.CPUOnly/lib/net45/x64/"
        "./packages/CNTK.CPUOnly/support/x64/Release/"
        "./packages/CNTK.Deps.MKL/support/x64/Dependency/"
        "./packages/CNTK.Deps.OpenCV.Zip/support/x64/Dependency/"
        "./packages/CNTK.Deps.OpenCV.Zip/support/x64/Dependency/Release"
        @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v9.0\bin"
        @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v9.1\bin"
    ]

dependencies 
|> Seq.iter (fun dep -> 
    let path = Path.Combine(__SOURCE_DIRECTORY__,dep)
    Environment.SetEnvironmentVariable("Path",        
        Environment.GetEnvironmentVariable("Path") + ";" + path)
    )    

do
    [   // https://docs.microsoft.com/en-us/cognitive-toolkit/windows-environment-variables
        //"BOOST_INCLUDE_PATH", @"c:\local\boost_1_60_0-msvc-14.0"
        //"BOOST_LIB_PATH", @"c:\local\boost_1_60_0-msvc-14.0\lib64-msvc-14.0"
        //"CNTK_OPENBLAS_PATH", @"c:\local\CNTKopenBLAS OpenBLAS library"
        //"CUB_PATH", @"c:\local\cub-1.7.4"
        //"CUDNN_PATH", @"C:\local\cudnn-9.0-v7.0\cuda" 
        "CUDA_PATH", @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v9.0"
        "CUDA_PATH_V9_0", @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v9.0" 
        "CUDA_PATH_V9_1", @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v9.1" 
        //"MKL_PATH", @"C:\local\mklml-mkldnn-0.12"
        //"OPENCV_PATH_V31", @"c:\local\Opencv3.1.0\build"
        //"PROTOBUF_PATH", @"c:\local\protobuf-3.1.0-vs17"
        //"SWIG_PATH", @"C:\local\swigwin-3.0.10"
        //"ZLIB_PATH", @"c:\local\zlib-vs17"
        ]
    |> List.iter ( fun (variable,value) -> 
        match Environment.GetEnvironmentVariable(variable) with
        | null -> Environment.SetEnvironmentVariable(variable,value)
        | v ->
            if String.Compare (v,value) = 0 then 
                printfn "%s path is set %s %s" variable v value
            else
                Environment.SetEnvironmentVariable(variable,value))

#I "./packages/CNTK.CPUOnly/lib/net45/x64/"
#I "./packages/CNTK.CPUOnly/support/x64/Release/"

#r "./packages/CNTK.CPUOnly/lib/net45/x64/Cntk.Core.Managed-2.4.dll"
// #I "./packages/CNTK.GPU/lib/net45/x64/"
// #I "./packages/CNTK.GPU/support/x64/Release/"

// #r "./packages/CNTK.GPU/lib/net45/x64/Cntk.Core.Managed-2.4.dll"
