---
title: Configuration
description: All the different ways to configure the MPF game logic engine
---

# Configuration

The `MpfGamelogicEngine` component offers many configuration options. For a
brief description, you can refer to the tooltips that are displayed when you
hover your cursor above an option in the Unity inspector.

## Executable source

Select which version of MPF VPE should launch on startup.

- **Included:** Use the version of MPF included in VPE. It is slightly
  [different](technical-details.md#included-mpf-binaries) from the official
  version.
- **Manually Installed:** Use the locally installed version of MPF available
  through the system `PATH` environment variable.
- **Assume Running:** Do not launch MPF at all. Assume it is running already.
  This option is intended for debugging MPF.

### Manual installation

> [!WARNING]
>
> If you choose manual installation of MPF, anyone who wants to play your table
> will need to install MPF first.

If you want to use a version of MPF other than the included one, you can install
it manually. Refer to the
[official installation instructions](https://missionpinball.org/latest/install/).
The earliest version of MPF compatible with VPE is `v0.55.0-dev.12`.

## Startup behavior

Select how VPE establishes a connection with MPF.

- **Ping Until Ready**: Repeatedly send a _Ping_ message to MPF until it
  responds. This is the preferred method, but it only works with the included
  version of MPF. If MPF does not respond within the timeframe specified by the
  _Connect Timeout_ option, VPE will give up and cause an error.
- **Delay Connection**: Delay the _Start_ message by a number of seconds
  specified by the _Connect Delay_ option assuming MPF is ready to receive it by
  then. If MPF is not ready, startup will fail and the table will not work. If
  MPF starts up faster, this delay slows startup needlessly. This method is
  compatible with all version of MPF since `v0.55.0-dev.12`.

## Machine folder

The file path to the
[_machine folder_](https://missionpinball.org/latest/tutorial/2_creating_a_new_machine/)
of your table. When the package is first installed, it will create a default
machine folder in the `Assets/StreamingAssets` directory of your Unity project.
The path to this directory is the default setting. You can store your machine
folder elsewhere, but be aware that the
[`StreamingAssets`](https://docs.unity3d.com/Manual/StreamingAssets.html)
directory is special. Unlike files located in other parts of your Unity project,
Unity will not convert its contents into a binary format when you build your
Unity project into a standalone application. It will therefore remain readable
for MPF. It will also not go missing if you move your Unity project or a build
of your Unity project to another computer.

## Media controller

Select the media controller MPF should attempt to connect to on startup.

- **None:** Do not connect to any media controller
- **Godot or Legacy Mc:** Use the officially supported media controller of
  whatever MPF version is running. MPF versions since `v0.80` will attempt to
  start and connect to the new
  [Godot-based media controller](https://missionpinball.org/latest/gmc/). MPF
  versions prior to `v0.80` will attempt to start and connect to the legacy
  [Kivvy-based media controller](https://missionpinball.org/latest/mc/). The
  version of MPF included in VPE is `v0.80.0`, so it will use Godot. If you
  choose this option, you must manually install the appropriate media
  controller.
- **Other:** Do not start any particular media controller, but attempt to
  connect to an already running media controller. The IP and port to connect to
  can be configured in MPF. If you choose this option, you are responsible for
  starting a media controller.

## Output type

Select the format and presentation of MPFs output.

- **None:** MPF will not produce any live output. It will still produce log
  files.
- **Table in terminal:** A command line table view that shows you the active
  modes and the states of your switches.
- **Log in terminal:** Log events and errors to a terminal window as they
  happen.
- **Log in Unity console:** Log events and errors to the Unity console as they
  happen. Errors may not always be labeled as such.

## Verbose logging

Enable this option to get additional information from MPF. Sometimes useful for
debugging. Not advisable on Windows in combination with the _Table in terminal_
_Output type_ because the Windows terminal is slow.

## Cache config files

Whether or not to cache config files to speed up the next startup. MPF will
always parse config files that have been changed.

## Force reload config

From the
[MPF documentation](https://missionpinball.org/latest/running/commands/game/):

> Forces MPF to reload the config from the actual YAML config files, rather than
> from cache. MPF contains a caching mechanism that caches YAML config files,
> and if the original files haven't changed since the last time MPF was run, it
> loads them from cache instead. Cached files are stored in your machine's temp
> folder which varies depending on your system.

## Force load all assets on start

From the
[MPF documentation](https://missionpinball.org/latest/running/commands/game/):

> Forces MPF to load all assets at start (rather than the default behavior where
> some assets can be loaded only when modes start or based on other events).
> This is useful during development to ensure that all assets are valid and
> loadable.
