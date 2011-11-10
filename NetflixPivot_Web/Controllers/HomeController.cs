using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;

namespace NetflixPivot_Web.Controllers
{
    public class HomeController : Controller
    {
        private Uri GetBlobOrCdnUri(CloudBlob blob, string cdnHost)
        {
            // always use HTTP to avoid Silverlight cross-protocol issues
            var ub = new UriBuilder(blob.Uri)
            {
                Scheme = "http"
            };
            if (ub.Port == 443) ub.Port = 80; // Adjust to port 80 only if port was already 443, or (below) if we're using a CDN
            if (!string.IsNullOrEmpty(cdnHost))
            {
                ub.Host = cdnHost;
                ub.Port = 80;
            }
            return ub.Uri;
        }

        public ActionResult Index()
        {
            var blobs = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString")).CreateCloudBlobClient();

            var cdnHost = RoleEnvironment.GetConfigurationSettingValue("CdnHost");

            var controlBlob = blobs.GetBlobReference("control/NetflixPivotViewer.xap");
            var collectionBlob = blobs.ListBlobsWithPrefix("collection/collection-").OfType<CloudBlob>().Where(b => b.Uri.AbsolutePath.EndsWith(".cxml")).FirstOrDefault();

            if (collectionBlob == null)
            {
                return Content("Catalog not yet available. This can take up to an hour or so, depending on available bandwidth, disk I/O, and CPU.");
            }

            ViewData["xapUrl"] = GetBlobOrCdnUri(controlBlob, cdnHost).AbsoluteUri;
            ViewData["collectionUrl"] = GetBlobOrCdnUri(collectionBlob, cdnHost).AbsoluteUri;
            return View();
        }

        public ActionResult About()
        {
            return View();
        }
    }
}
