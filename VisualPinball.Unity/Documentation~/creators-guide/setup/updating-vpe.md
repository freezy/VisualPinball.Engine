# Updating VPE

VPE is under heavy development, so it's frequently updated, usually multiple times per week. In order to not have to delete your existing `VisualPinball.Engine` folder and download and extract the code each time, we recommend using git.

Git is a distributed version control system. It's very sophisticated but can also be a bit overwhelming to use. However, with the cheat sheet below you should be able to handle it.

First you need to [download git](https://git-scm.com/downloads). Make sure it's in your `PATH` environment variable. There are free GUIs for git such as [Fork](https://git-fork.com/), [GitKraken](https://www.gitkraken.com/) or [Source Tree](https://www.sourcetreeapp.com/), but we'll focus on the command line version on Windows here. Linux and macOS are similar but use a command shell or terminal window.

Open a command prompt by pressing the Windows key and typing `cmd`, followed by enter. Make sure that git is installed by typing `git --version`. This should return something like `git version 2.18.0.windows.1`. 

Next, go to the folder where you want to have VPE installed. If there is already a folder where you've extracted VPE from before, delete it. 

Following the recommended file structure, you would type:

```cmd
cd %userprofile%\VPE
git clone https://github.com/freezy/VisualPinball.Engine.git
```

This downloads the latest version of VPE into `%userprofile%\VPE\VisualPinball.Engine` and keeps a link to GitHub. In the future, if you want to update VPE, it's simply a matter of going into the folder and "pull" the changes:

```cmd
cd %userprofile%\VPE\VisualPinball.Engine
git pull
```

However, you might have experimented in the VPE folder to test out stuff, and git complains it can't update. Here is a way to discard all local changes and pull in what's on GitHub:

```cmd
git fetch --prune
git checkout -- **
git reset --hard origin/master
```

> [!WARNING]
> Should you have *committed* changes (as in, you've developed something, and added and commited it to git), this will also discard those changes. But if you have done that you're probably a seasoned developer and know what you're doing, right? :)
