![Lawnchair Logo](https://www.iconfinder.com/icons/66420/download/png/48)
*"Launcher"*

## About
### What?

A portable app for launching scripts. It is built in C# WPF on .NET 4.5.2.
```diff
- WARNING: Lawnchair's maturity is only at the level of a proof-of-concept. You may encounter bugs.
```

### Why?

I needed a way to get less-technical admins to run PowerShell scripts we wrote and curated for them. I also needed to ensure consistency in how they were ran and what versions of the scripts were ran.

### How?

1. Lawnchair can look at one or more paths for metadata (eg. local paths, mapped drive, UNC paths)
2. Lawnchair discovers JSON metadata files
3. These metadata files contain information like script friendly names, version, author, paths, etc
4. This metadata becomes searchable in a fairly intuitive interface
5. The folder structure the metadata resides in determines the categories
6. Users can quickly search by a number of different fields for a script
7. Users can double click or arrow to and press `ENTER` to launch the script
8. Script author comments can be optionally presented before launching

### But?

#### Why does't Lawnchair wrap a GUI around the scripts? (eg. Provide a results pane, grid view, etc)?
Lawnchair's original scope was to find and launch scripts. The user experience during execution (and working with the results after) is deferred to the script authors. This is very much intentional as I've had to work with a myriad of different scripts/solutions from different authors that do not confirm to a single style or approach.

Fun fact! Some tools, like SQL scripts manager (can do PowerShell and Python as well) (https://www.red-gate.com/products/dba/sql-scripts-manager/) offer functionality for working with script output/results and logging. If you're looking for something more than just a launcher, I recommend exploring SQL scripts manager.

#### Why use Lawnchair over ADO pipelines, Jenkins, Control-M, etc?
Executors/schedulers typically do not expose the shell to the user. For scripts that prompt for input from the user *during* execution (not just at the beginning) wont work with these kinds of tools.

## Usage
### Requirements
- Windows 7 or newer (I've only tested Windows 10)
- .NET Framework 4.5.2 or newer

### Setup
1. Find where you want your script metadata files to live  
   > Note: This can be one or more local, mapped drive or UNC paths  
   
   > Note: Script metadata files tell Lawnchair where to find scripts. They also describe information about the scripts like author, friendly name, version and the path to the script. These metadata files do not have to exist in the same location as your scripts, they could be on a completely different drive or UNC path if needed.
2. Set up the folder structure however you like.
   > Note: By default, folder names dictate the categorie names you see in Lawnchair. For instance, if you place a `lawnchair_metadata.json` file in `\My PowerShell Scripts\testing` the category for whatever scripts that file describes will be `My Powershell Scripts\testing`. This behavior can be overridden if desired.
3. Create one or more metadata files using the below templates. Save the files anywhere in your metadata repo with filenames of `lawnchair_metadata.json`
   > Note: Lawnchair by default looks for metadata files with the filename `lawnchair_metadata.json`. If this needs to be changed to something else look at the settings file created by Lawnchair upon first run (more on that on step #5)  
   
   > Note: A metadata file is not limited to a single script. If you wanted to define multiple scripts you can (example is below). This is useful for scripts that share something in common or that you want to manage with a minimal set of metadata files.
   
#### PowerShell example
```
{
  "Author": "the name of the script author",
  "Category": "@category",
  "Comments": "These are comments the script author would create.",
  "Name": "a friendly name for your script",
  "ScriptArguments": "-ExecutionPolicy unrestricted -NoProfile -File \"@scriptFullPath\"",
  "ScriptExecutor": "powershell",
  "ScriptRelativePath": "\\script.ps1",
  "ScriptRootPath": "@scriptRootPath",
  "Version": "1.1"
}
```
#### Python example
```
{
  "Author": "the name of the script author",
  "Category": "@category",
  "Comments": "These are comments the script author would create.",
  "Name": "a friendly name for your script",
  "ScriptArguments": "\"@scriptFullPath\"",
  "ScriptExecutor": "python",
  "ScriptRelativePath": "\\script.py",
  "ScriptRootPath": "@scriptRootPath",
  "Version": "0.76"
}
```
#### Combination example
```
[
  {
    "Author": "the name of the script author",
    "Category": "@category",
    "Comments": "These are comments the script author would create.",
    "Name": "a friendly name for your script",
    "ScriptArguments": "-ExecutionPolicy unrestricted -NoProfile -File \"@scriptFullPath\"",
    "ScriptExecutor": "powershell",
    "ScriptRelativePath": "\\script.ps1",
    "ScriptRootPath": "@scriptRootPath",
    "Version": "1.1"
  },
  {
    "Author": "the name of the script author",
    "Category": "@category",
    "Comments": "These are comments the script author would create.",
    "Name": "a friendly name for your script",
    "ScriptArguments": "\"@scriptFullPath\"",
    "ScriptExecutor": "python",
    "ScriptRelativePath": "\\script.py",
    "ScriptRootPath": "@scriptRootPath",
    "Version": "0.76"
  }
]
```
| Field  | Type | Comments |
| ------ | -----| -------- |
| Author | String | The author of the script (eg. person's name/email, team name, etc) |
| Category | String | If set to `@category` Lawnchair will substitute in the folder path (from the root) to the metadata file as the category name. You can change this to explicitly show another category name |
| Comments | String | This is shown to the user right before they launch the script |
| Name | String | A friendly name for the script |
| ScriptArguments | String | If it contains to `@scriptFullPath` Lawnchair will substitute in the `full\path\to\script\file.xyz`. You can change this to add additional hardcoded parameters |
| ScriptExecutor | String | The executor which will run the script. You do not need to supply the full path to the executor if the directory the executor is in is included in the `%PATH%` environmental variable |
| ScriptRelativePath | String | The second half of the path to the script, starting from where `ScriptRootPath` ended |
| ScriptRootPath | String | The first half of the path to the script. If set to `@scriptRootPath` Lawnchair will substitute in the folder path to the metadata file. You can change this to another path (useful if your metadata files do not exist in the same repo your scripts do). For instance, `ScriptRootPath` set to `c:\\temp` and `ScriptRelativePath` set to `\\testing\\script.ps1` will result in a call to PowerShell with a script path of `c:\temp\testing\script.ps1` |
| Version | String | A simple version number |

4. Download the latest Lawnchair executable. See the [releases page here](https://github.com/compelx/lawnchair/releases/tag/20191210001) .
5. Run Lawnchair - it will prompt you to select the location of your metadata folder.
   Lawnchair will recursively search that folder (and child folders) for metadata files.
   > Note: If you have multiple metadata repositories then edit `%localappdata%\lawnchair\settings.json` including an additional path in the `ScriptRepositoryPaths` array. If a `settings.json` already exists in `%localappdata%\lawnchair` then Lawnchair will use it instead -- this is useful for predefining settings for other users although you will need to figure out a way to not only deliver the Lawnchair executable to them but also create the folder `%localappdata%\lawnchair` and copy the predefined `settings.json` into it.


###### Links, Attribution
- Lawnchair icon  
  Author: Visual Pharm http://icons8.com/  
  Sourced from https://www.iconfinder.com/icons/66420/beach_chair_hairy_summer_vacation_icon  
  License https://creativecommons.org/licenses/by-nd/3.0/
