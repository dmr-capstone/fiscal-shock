# Rules for Contribution
The following are the rules of contribution.
## Branches
Please do not push to master. While master has been locked down to not allow pushing, force pushing may still work. *Please do not risk it.*

Please prefix branches with feature or bugfix.
## Formatting
- Blocks of code should be formatted so that each nested block is tabbed forward another set of spaces.
- For tabbing, use four *spaces*. This can be edited in your preferred text editor.
- The following naming conventions apply:
    - Use PascalCase for classes.
    - Use uppercase SNAKE_CASE for constant variables. This does not mean literal constants (i.e. JavaScript's "const" keyword), but functional constants (i.e. variables whose values determine magic numbers, file names, and other such unchanging values). If the value of the variable or any values in an object change, it is not truly a constant.
    - Use camelCase for any functions, variables, or other identifiers that do not fall into the previous two categories.
## Editing
Feel free to use any text editor or IDE that you like, but please look up the gitignore file for your preferred editor. You can find different gitignore setups for different editors at the following link: https://gitignore.io/