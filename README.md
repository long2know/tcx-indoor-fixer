# tcx-indoor-fixer
Helps fix tcx/gpx files that are from indoor activities.

This is a pretty simple .NET Core console applications that will take a TCX/GPX file as an input, and output it as a TCX file that has had all of the GPS data stripped and had the distance/speed nodes corrected.

The purpose is to allow one to record a GPS-enabled workout, indoors, but then treat it as an indoor workout to retain Heart Rate and other data.

Other inputs to the console application allow specifying total distance and total time.
