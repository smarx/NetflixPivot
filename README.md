Source code for <http://netflixpivot.cloudapp.net>.

Prerequisites
-------------
To run this, you'll need the [Windows Azure SDK](http://windowsazure.com/sdk) as well as the binaries for the [PivotViewer Control](http://www.silverlight.net/learn/data-networking/pivot-viewer/pivotviewer-control)
and [Deep Zoom Tools](http://seadragon.com/developer/creating-content/deep-zoom-tools/). (You may need to update the references in the NetflixPivotViewer project to point
to your location of those binaries.)

Deploying
---------
To deploy to Windows Azure, update the connection strings in `ServiceConfiguration.cloud.cscfg` as well as (optionally) the `CdnUrl` which should point to a CDN URL that
sits on top of your storage account. (It should look like `az####.vo.msecnd.net`, or a custom domain if you've mapped one to your CDN URL.)