namespace Haya.Core

type DataFormat = | Json | Yaml 

type CrcCommand = {
    PathToSln: string
    OutputPath: string
    IncludeL1Diagram: bool
    IncludeL2Diagram: bool
    CurrentDirectory: string
}
