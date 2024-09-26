# Decision Log

## Decision: Separate Commands for Different Formats

Date: 2024-09-26  
Status: Accepted  
Author: dburriss

### Context 
`crc` was outputting md, json, and yaml in the same command.
It also had option of adding C4 level 1 and 2 diagrams, which was not 
relevant for the json and yaml output.

### Decision
Separate the commands for different formats, rather than trying to fit them into the crc command.
- `describe` for json and yaml output.
- `diagram` for C4 level 1 and 2 diagrams with various formats supported (initially mermaid).
- `backstage` for outputting a backstage catalog spec.

### Consequences
- *Pros:*
  - Easier to understand what each command does.
  - Easier to add new formats/functionality.
  - Simplifies the codebase.
- *Cons:*
  - More commands to remember.
  - More commands to document.
