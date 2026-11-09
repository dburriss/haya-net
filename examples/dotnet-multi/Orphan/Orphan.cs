using Haya;
namespace Orphan;
[Responsibility(Description = "Unreferenced project")]
public class Lonely { }

// A class carrying both attributes must yield one responsibility and one collaborator (regression test).
[Responsibility(Description = "Does both")]
[Collaborator(Description = "Also collaborates", AppName = "Elsewhere")]
public class Both { }
