(*
Fictional case, to explore:
1. Multiple input variables,
2. Forks / Joins of functions.

2 input variables, v1 and v2
Model:

v1 --- f1:dense 
                \
                   f4:dense
                /           \
       f2:dense              \
     /                        \
v2 -               f5:dense ---- f7:output  
     \           /            /
       f3:dense -            /
                 \          /
                   f6:dense
*)

#load "../ScriptLoader.fsx"
open CNTK

#r "../build/CNTK.FSharp.dll"
open CNTK.FSharp

open System.IO
open System.Collections.Generic


let var f = new Variable(f)

let v1 = Variable.InputVariable(shape [ 2 ], DataType.Float, "v1")
let v2 = Variable.InputVariable(shape [ 3 ], DataType.Float, "v2")

// lifted from other samples

let fullyConnectedLinearLayer(
    input:Variable, 
    outputDim:int, 
    device:DeviceDescriptor,
    outputName:string) : Function =

        let inputDim = input.Shape.[0]

        let timesParam = 
            new Parameter(
                shape [outputDim; inputDim], 
                DataType.Float,
                CNTKLib.GlorotUniformInitializer(
                    float CNTKLib.DefaultParamInitScale,
                    CNTKLib.SentinelValueForInferParamInitRank,
                    CNTKLib.SentinelValueForInferParamInitRank, 
                    uint32 1),
                device, 
                "timesParam")

        let timesFunction = 
            var (CNTKLib.Times(timesParam, input, "times"))

        let plusParam = new Parameter(shape [ outputDim ], 0.0f, device, "plusParam")
        CNTKLib.Plus(plusParam, timesFunction, outputName)

let device = DeviceDescriptor.CPUDevice

let f1 = fullyConnectedLinearLayer(v1, 2, device, "function1")
let f2 = fullyConnectedLinearLayer(v2, 3, device, "function2")
let f3 = fullyConnectedLinearLayer(v2, 4, device, "function3")

let f4  = 

    let outputDim = 2

    let input1 = new Variable(f1)
    let input2 = new Variable(f2)

    let input1Dim = input1.Shape.[0]

    let weights1 = 
        new Parameter(
            shape [outputDim; input1Dim], 
            DataType.Float,
            CNTKLib.GlorotUniformInitializer(
                float CNTKLib.DefaultParamInitScale,
                CNTKLib.SentinelValueForInferParamInitRank,
                CNTKLib.SentinelValueForInferParamInitRank, 
                uint32 1),
            device, 
            "weights1")

    let times1Function = 
        var (CNTKLib.Times(weights1, input1, "times1"))

    let input2Dim = input2.Shape.[0]

    let weights2 = 
        new Parameter(
            shape [outputDim; input2Dim], 
            DataType.Float,
            CNTKLib.GlorotUniformInitializer(
                float CNTKLib.DefaultParamInitScale,
                CNTKLib.SentinelValueForInferParamInitRank,
                CNTKLib.SentinelValueForInferParamInitRank, 
                uint32 1),
            device, 
            "weights2")

    let times2Function = 
        var (CNTKLib.Times(weights2, input2, "times2"))
    
    let total = CNTKLib.Plus(times1Function,times2Function)
    let bias = new Parameter(shape [ outputDim ], 0.0f, device, "plusParam")

    CNTKLib.Plus(bias, var (total), "function4")

let f5 = fullyConnectedLinearLayer(new Variable(f3), 2, device, "function5")
let f6 = fullyConnectedLinearLayer(new Variable(f3), 3, device, "function6")

let output =

    // f4 + f5 + f6
    let outputDim = 3

    let input1 = var f4
    let input2 = var f5
    let input3 = var f6

    let input1Dim = input1.Shape.[0]

    let weights1 = 
        new Parameter(
            shape [outputDim; input1Dim], 
            DataType.Float,
            CNTKLib.GlorotUniformInitializer(
                float CNTKLib.DefaultParamInitScale,
                CNTKLib.SentinelValueForInferParamInitRank,
                CNTKLib.SentinelValueForInferParamInitRank, 
                uint32 1),
            device, 
            "weights1")

    let times1Function = 
        var (CNTKLib.Times(weights1, input1, "times1"))

    let input2Dim = input2.Shape.[0]

    let weights2 = 
        new Parameter(
            shape [outputDim; input2Dim], 
            DataType.Float,
            CNTKLib.GlorotUniformInitializer(
                float CNTKLib.DefaultParamInitScale,
                CNTKLib.SentinelValueForInferParamInitRank,
                CNTKLib.SentinelValueForInferParamInitRank, 
                uint32 1),
            device, 
            "weights2")

    let times2Function = 
        var (CNTKLib.Times(weights2, input2, "times2"))

    let input3Dim = input3.Shape.[0]

    let weights3 = 
        new Parameter(
            shape [outputDim; input3Dim], 
            DataType.Float,
            CNTKLib.GlorotUniformInitializer(
                float CNTKLib.DefaultParamInitScale,
                CNTKLib.SentinelValueForInferParamInitRank,
                CNTKLib.SentinelValueForInferParamInitRank, 
                uint32 1),
            device, 
            "weights3")

    let times3Function = 
        var (CNTKLib.Times(weights3, input3, "times3"))

    let total = var (times1Function + times2Function) + times3Function
    let bias = new Parameter(shape [ outputDim ], 0.0f, device, "plusParam")

    CNTKLib.Plus(bias, var (total), "output")

// output is the function we need to learn
// let's create a synthetic dataset
// 5 input, split across 2 for v1 and 3 for v2
// 3 outputs

let rng = System.Random(0)

let fake (xs:float[]) =
    let value = 
        sin xs.[0] * cos xs.[1] + xs.[2] * xs.[3] - xs.[4] ** 0.7
    if value > 0.5 then 0
    elif value > 0.2 then 1
    else 2

let createSample () = 
    let input = Array.init 5 (fun _ -> rng.NextDouble ())
    input, fake input

[ 0 .. 99 ]
|> List.map (fun _ -> createSample ())
|> List.map snd
|> Seq.countBy id

let createFile () = 
    Seq.init 1000 (fun _ -> createSample())
    |> Seq.map (fun (features,labels) ->
        let feat1 = sprintf "|feature1 %.2f %.2f" features.[0] features.[1]
        let feat2 = sprintf "|feature2 %.2f %.2f %.2f" features.[2] features.[3] features.[4]
        let labs = 
            if labels = 0 then "|labels 1 0 0"
            elif labels = 1 then "|labels 0 1 0"
            else "|labels 0 0 1"
        [labs;feat1;feat2] |> String.concat " "
        )
    |> fun content -> System.IO.File.WriteAllLines(__SOURCE_DIRECTORY__ + "/data", content)

// createFile () //to generate data file

// learn

let labels = CNTKLib.InputVariable(shape [ 3 ], DataType.Float, "labels")

let trainingLoss = CNTKLib.CrossEntropyWithSoftmax(var (output), labels, "lossFunction")
let prediction = CNTKLib.ClassificationError(var (output), labels, "classificationError")

let streamConfigurations = 
    ResizeArray<StreamConfiguration>(
        [
            new StreamConfiguration("feature1", 2)    
            new StreamConfiguration("feature2", 3)
            new StreamConfiguration("labels", 3)
        ]
        )

let minibatchSource = 
    MinibatchSource.TextFormatMinibatchSource(
        Path.Combine(__SOURCE_DIRECTORY__, "data"), 
        streamConfigurations, 
        MinibatchSource.InfinitelyRepeat)

let feature1StreamInfo = minibatchSource.StreamInfo("feature1")
let feature2StreamInfo = minibatchSource.StreamInfo("feature2")
let labelStreamInfo = minibatchSource.StreamInfo("labels")

// set per sample learning rate
let learningRatePerSample : CNTK.TrainingParameterScheduleDouble = 
    new CNTK.TrainingParameterScheduleDouble(0.001, uint32 1)

let parameterLearners = 
    ResizeArray<Learner>(
        [
            Learner.SGDLearner(output.Parameters(), learningRatePerSample)
        ]
        )

let trainer = Trainer.CreateTrainer(output, trainingLoss, prediction, parameterLearners)

let minibatchSize = uint32 64
let outputFrequencyInMinibatches = 20

// output.Parameters () |> Seq.toArray
// output.Arguments |> Seq.toArray // variables
// output.Inputs |> Seq.toArray
// output.Outputs |> Seq.toArray
// output.Output

type Specification = {
    Features: Variable seq
    Labels: Variable
    Model: Function
    Loss: Loss
    Eval: Loss
    }

module Minibatch = 

    let prepare 
        (source: MinibatchSource)
        (spec:Specification)
        (mapping: Map<string,string>)
        (batch:UnorderedMapStreamInformationMinibatchData)
        : IDictionary<Variable,MinibatchData> =
            let fromStream info = batch.[info]
            [
                for feature in spec.Features ->
                    feature, 
                    source.StreamInfo(mapping.[feature.Name]) 
                    |> fromStream
                yield 
                    spec.Labels, source.StreamInfo(mapping.[spec.Labels.Name]) 
                    |> fromStream      
            ]
            |> dict

    let trainOn (trainer:Trainer) (device:DeviceDescriptor) (minibatch:IDictionary<Variable,MinibatchData>) =
        trainer.TrainMinibatch(minibatch, device)

let map = 
    [
        "v1", "feature1"
        "v2", "feature2"
        "labels", "labels"
    ] 
    |> Map.ofSeq

let spec = {
    Features = [ v1; v2 ]
    Labels = labels
    Model = output
    Loss = CrossEntropyWithSoftmax
    Eval = ClassificationError
    }

let learn epochs =

    let prepare = Minibatch.prepare minibatchSource spec map
    let train = Minibatch.trainOn trainer device

    let rec learnEpoch (step,epoch) = 

        if epoch <= 0
        // we are done
        then ignore ()
        else
            let step = step + 1

            let minibatch = minibatchSource.GetNextMinibatch(minibatchSize, device)
            
            let _ = 
                minibatch
                |> prepare
                |> train

            trainer
            |> Minibatch.summary 
            |> Minibatch.basicPrint        
            
            // MinibatchSource is created with MinibatchSource.InfinitelyRepeat.
            // Batching will not end. Each time minibatchSource completes an sweep (epoch),
            // the last minibatch data will be marked as end of a sweep. We use this flag
            // to count number of epochs.
            let epoch = 
                if (Minibatch.isSweepEnd(minibatch))
                then epoch - 1
                else epoch

            learnEpoch (step,epoch)

    learnEpoch (0,epochs)

let epochs = 50
learn epochs

output.Save(__SOURCE_DIRECTORY__ + "/model")



// validate the model
let minibatchSourceNewModel = 
    MinibatchSource.TextFormatMinibatchSource(
        Path.Combine(__SOURCE_DIRECTORY__ + "/data"), 
        streamConfigurations, 
        MinibatchSource.FullDataSweep)

// broken: can't retrieve the inputs!?
let ValidateModelWithMinibatchSource(
    modelFile:string, 
    testMinibatchSource:MinibatchSource,
    outputName:string,
    device:DeviceDescriptor, 
    maxCount:int
    ) =

        let model : Function = Function.Load(modelFile, device)

        let feat1 = 
            model.Inputs
            |> Seq.filter (fun i -> i.Name = "v1")
            |> Seq.exactlyOne

        let feat2 = 
            model.Inputs
            |> Seq.filter (fun i -> i.Name = "v2")
            |> Seq.exactlyOne

        let labelOutput = 
            model.Outputs 
            |> Seq.filter (fun o -> o.Name = outputName)
            |> Seq.exactlyOne

        let feature1StreamInfo = testMinibatchSource.StreamInfo("feature1")
        let feature2StreamInfo = testMinibatchSource.StreamInfo("feature2")
        let labelStreamInfo = testMinibatchSource.StreamInfo("labels")

        let batchSize = 50

        let rec countErrors (total,errors) =

            printfn "Total: %i; Errors: %i" total errors

            let minibatchData = testMinibatchSource.GetNextMinibatch((uint32)batchSize, device)

            if (minibatchData = null || minibatchData.Count = 0)
            then (total,errors)        
            else

                let total = total + minibatchData.[labelStreamInfo].numberOfSamples

                // find the index of the largest label value
                let labelData = minibatchData.[labelStreamInfo].data.GetDenseData<float32>(labelOutput)
                let expectedLabels = 
                    labelData 
                    |> Seq.map (fun l ->                         
                        let largest = l |> Seq.max
                        l.IndexOf largest
                        )

                let inputDataMap = 
                    [
                        feat1, minibatchData.[feature1StreamInfo].data
                        feat2, minibatchData.[feature2StreamInfo].data
                    ]
                    |> dataMap

                let outputDataMap = 
                    [ 
                        labelOutput, null 
                    ] 
                    |> dataMap
                    
                model.Evaluate(inputDataMap, outputDataMap, device)

                let outputData = outputDataMap.[labelOutput].GetDenseData<float32>(labelOutput)
                let actualLabels =
                    outputData 
                    |> Seq.map (fun l ->                         
                        let largest = l |> Seq.max
                        l.IndexOf largest
                        )

                let misMatches = 
                    (actualLabels,expectedLabels)
                    ||> Seq.zip
                    |> Seq.sumBy (fun (a, b) -> if a = b then 0 else 1)

                let errors = errors + misMatches

                if (int total > maxCount)
                then (total,errors)
                else countErrors (total,errors)

        countErrors (uint32 0,0)

let total,errors = 
    ValidateModelWithMinibatchSource(
        (__SOURCE_DIRECTORY__ + "/model"), 
        minibatchSourceNewModel,
        "output",
        device,
        1000)

// this is bizarre - input gives nothing, arguments does.
let model : Function = Function.Load((__SOURCE_DIRECTORY__ + "/model"), device)
model.Inputs |> Seq.map (fun x -> printfn "%A" x)
model.Arguments|> Seq.map (fun x -> printfn "%A" x.Name)

printfn "Total: %i / Errors: %i" total errors
