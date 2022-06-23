---
uid: tutorial_backglass_1
title: Realistic Looking Backglass - Create Artwork
description: How to edit art in Photoshop
---

# Finding Artwork

Perhaps the most challenging aspect to making a working backglass is finding good artwork.  The best possible art is a direct scan of the backglass but that is not always possible.  There are backglass images that can be found online but it takes a fair amount of time to clean these up and remove any artifact from them.  Another possibility is to get artwork online and use that as a basis for a total redraw.

# Editing the Colored Layer

Once you have your artwork secured save this as a png file with the name of your choice.   Make sure that the areas for the score reels and credit reel set as transparent.  Be sure to size the backglass image to the size of the original backlgass, in the case of Grand Tour this is 21" x 22".

![Color Edit](FullColorBG.jpg)

# Editing the Thickness Mask layer

To block the passage of light through the backglass we need to make a thickness map layer.  This layer consists of pure white (255,255,255) for the areas that are opaque and pure black (0,0,0) for the areas that are transparent.  If you have a scan of the backglass this is easy to set up.  If you don't have a scan then you'll have to find a font that matches the original and layout all of the mask artwork.  One way to find fonts is to use an online "what's my font" service.  Another option is to hand draw the lettering since all of this artwork was hand drawn to begin with.  Once you are done, save this as a png as well

![Thickness Mask Edit](ThicknessMap.jpg)

You're now ready to [create a backglass mesh](xref:tutorial_backglass_2).
