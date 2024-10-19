module Haya.Core.IO

module IO =
    open System.IO
    let write file data =
        // TODO: check file extension
        File.WriteAllText(file, data)
        Ok(file)
        
    let fileExists file = File.Exists(file)
