# How to Contribute

You want to contribute to VPE? Awesome! Here are a few things you should know.

## Submitting Changes

We rarely commit to master directly. Instead, we open [pull requests](https://docs.github.com/en/free-pro-team@latest/github/collaborating-with-issues-and-pull-requests/about-pull-requests)
and let our peers review the code before it gets merged.

- We [rebase](https://git-scm.com/book/en/v2/Git-Branching-Rebasing) our commits to master, so the commit history stays linear.
- When a PR is about user-facing changes, we [update the documentation](https://github.com/freezy/VisualPinball.Engine/wiki/Documentation#bigger-changes-or-new-content).
- If a PR contains notable changes, we also update the [changelog](CHANGELOG.md). Add your entry to the top of the appropriate section.
- We try to prefix our commit messages with a word that quickly tells the reader where the change happened. Examples are `editor`, `doc`, `<component-name>`, etc.
- Changes that touch the core project should be unit-tested. 

## Code Style 

We aren't too picky about code style. Just start reading our code and you'll get the hang of it. There
are a few rules though:

- We mostly use [C#'s naming conventions](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/naming-guidelines).
  That said, for `MonoBehaviours` we sometimes also use [Unity's style](https://github.com/raywenderlich/c-sharp-style-guide)
  that puts field names in camel case.
- For the Unity projects, we use one namespace per project. For the core project, it's a namespace per folder.  
- We use tabs for indentation.
- This is open source software. Consider the people who will read your code, and make it look nice for them. It's sort of like 
  driving a car: Perhaps you love doing donuts when you're alone, but with passengers the goal is to make the ride as smooth 
  as possible.

## Talk to us!

Have a look at the [VPF thread](https://www.vpforums.org/index.php?showtopic=43651) if you have any question. We also have a Discord
server for internal discussion.
