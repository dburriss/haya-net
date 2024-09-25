namespace Haya.Core

type CrcFormat = | Md = 1 | Json = 2
type CrcCommand = {
    PathToSln: string
    OutputPath: string
    Format: CrcFormat
    IncludeL1Diagram: bool
    IncludeL2Diagram: bool
    CurrentDirectory: string
}
