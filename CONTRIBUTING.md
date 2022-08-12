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

### Unity License Setup For Automated Testing

All Pull Request must pass an automated run of all the unit tests before merging. These will fail until your fork of the `VisualPinball.Engine` is configured with Unity's license information.
Most contributors will be using a Personal license and will need to request a key on behalf of GitHub. Professional license users can gather their key from the [Unity Subscriptions Page](https://id.unity.com/en/subscriptions) and skip to step 8.

1. Create a new branch. We will use the [Unity - Request Activation File](https://github.com/marketplace/actions/unity-request-activation-file) action to request an activation file
2. Create a file called `.github/workflows/activation.yml` and add the following workflow action defintion.
```
name: Acquire activation file
on: push
jobs:
  activation:
    name: Request manual activation file 
    runs-on: ubuntu-latest
    steps:
      # Request manual activation file
      - name: Request manual activation file
        id: getManualLicenseFile
        uses: game-ci/unity-request-activation-file@v2
      # Upload artifact (Unity_v20XX.X.XXXX.alf)
      - name: Expose as artifact
        uses: actions/upload-artifact@v2
        with:
          name: ${{ steps.getManualLicenseFile.outputs.filePath }}
          path: ${{ steps.getManualLicenseFile.outputs.filePath }}
```
3. Commit and Push the new file.
4. Navigate to the Actions Tab of GitHub.
5. Once the action has completed download the manual activation file that now appeared as an artifact and extract the `Unity_v20XX.X.XXXX.alf` file from the zip.
6. Visit (license.unity3d.com) and upload the `Unity_v20XX.X.XXXX.alf` file.
7. You should now receive your license file (Unity_v20XX.x.ulf) as a download. It's ok if the numbers don't match your Unity version exactly.
8. Open `Github` > `<Your repository>` > `Settings` > `Secrets`
- Create the following secrets
  - `UNITY_EMAIL` - (Add the email address that you use to login to Unity)
  - `UNITY_PASSWORD` - (Add the password that you use to login to Unity)
- Personal License
  - `UNITY_LICENSE` - (Copy the contents of your ulf license file into here)
- Professional License
  - `UNITY_SERIAL` - (Add you serial key it should look like XX-XXXX-XXXX-XXXX-XXXX-XXXX)
  
9. You can delete the branch. This license can now be used by the automated build and test steps required for pull requests. 


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
