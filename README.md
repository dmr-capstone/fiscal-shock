# Team 12 - DMR Capstone

## Information
This is the capstone repository of Team 12, Spring 2020.

## Cloning
Please make sure to have your login information available when you clone the repository. It will ask you to login.

## Opening the Project for the First Time
Due to Unity's tedious nature with git control and script editing with outside editors, please do the following the first time you access the project:
1. Open the project in Unity FIRST.
2. From Unity, go to the assets and bring up the context menu by right clicking on a C# script inside the folder. Click on "Open C# Project".
3. The project will open in the text editor that you configured when you first set up Unity for scripting. Depending on your editor, you may not see certain files relevant to your project. You may have to go into your settings and turn off file exclusion for certain files. In our Google Drive, I have added a link to an example settings.json file exclusion settings inside of the VSCode FAQs file (Working -> Dev Environment -> VSCode FAQs).

The reason I recommend this course of action is because you may accidentally end up ignoring the .gitignore file inside the editor, which we may need to edit often as we get a hang of this project.

Additionally, if you are using Visual Studio Code, please ensure that you add the following line to the .csproj file that is generated the first time you open the project through Unity:
```xml
<PropertyGroup>
    <CodeAnalysisRuleSet>./roslynator.ruleset</CodeAnalysisRuleSet>
</PropertyGroup>
```
This may also work for other IDEs or text editors, but I am not really sure. If anyone decides to go with a different approach and successfully gets code analysis working, please add to the code analysis file under Working -> Dev Environment.

## Contributing
Please read the information in CONTRIBUTING.md before putting in a pull request. If you do not follow the rules, your build may fail and your pull request will be delayed until your contribution follows the rules.
