Note: You may run into issues where the Section is not rendered and instead you get cryptic errors about MEF, Composition, Reflection, Casting, etc.  In that case, you may just need
to delete this folder: %localappdata%\Microsoft\VisualStudio\14.0Exp\ComponentModelCache (assuming you are using VS2015 Experimental instance - adjust 14.0Exp if needed.  VS 2022 is "17")

Also note that for building and debugging the VS 2015 extension, you may need to use VS 2015.  Building and debugging the 2022 extension should be done with VS 2022