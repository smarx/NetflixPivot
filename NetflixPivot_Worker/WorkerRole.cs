using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.IO.Compression;

namespace NetflixPivot_Worker
{
    public class WorkerRole : RoleEntryPoint
    {
        private void UploadDirectoryRecursive(string path, CloudBlobContainer container)
        {
            string cxmlPath = null;

            // use 16 threads to upload
            Parallel.ForEach(EnumerateDirectoryRecursive(path),
                new ParallelOptions { MaxDegreeOfParallelism = 16 },
                (file) =>
            {
                // save collection-#####.cxml for last
                if (Path.GetFileName(file).StartsWith("collection-") && Path.GetExtension(file) == ".cxml")
                {
                    cxmlPath = file;
                }
                else
                {
                    // upload each file, using the relative path as a blob name
                    UploadFile(file, container.GetBlobReference(Path.GetFullPath(file).Substring(path.Length)));
                }
            });

            // finish up with the cxml itself
            if (cxmlPath != null)
            {
                UploadFile(cxmlPath, container.GetBlobReference(Path.GetFullPath(cxmlPath).Substring(path.Length)));
                UploadFile(cxmlPath, container.GetBlobReference("collection-current.cxml"));
            }
        }

        private IEnumerable<string> EnumerateDirectoryRecursive(string root)
        {
            foreach (var file in Directory.GetFiles(root))
                yield return file;
            foreach (var subdir in Directory.GetDirectories(root))
                foreach (var file in EnumerateDirectoryRecursive(subdir))
                    yield return file;
        }

        private void UploadFile(string filename, CloudBlob blob)
        {
            var extension = Path.GetExtension(filename).ToLower();
            if (extension == ".cxml")
            {
                // cache CXML for 30 minutes
                blob.Properties.CacheControl = "max-age=1800";
            }
            else
            {
                // cache everything else (images) for 2 hours
                blob.Properties.CacheControl = "max-age=7200";
            }
            switch (extension)
                {
                    case ".xml":
                    case ".cxml":
                    case ".dzc":
                        blob.Properties.ContentType = "application/xml";
                        break;
                    case ".jpg":
                        blob.Properties.ContentType = "image/jpeg";
                        break;
                }
            blob.UploadFile(filename);
        }

        public override void Run()
        {
            var container = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString")).CreateCloudBlobClient().GetContainerReference("collection");
            if (container.CreateIfNotExist())
            {
                container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            }

            while (true)
            {
                var scratchPath = RoleEnvironment.GetLocalResource("PivotScratch").RootPath;
                // clean up from previous iterations
                foreach (var dirname in Directory.GetDirectories(scratchPath))
                {
                    Directory.Delete(dirname, true);
                }

                // create the pivot collection locally
                NetflixPivotCreator.CreatePivotCollection(scratchPath.TrimEnd('\\'));

                Trace.WriteLine("Uploading everything.");
                UploadDirectoryRecursive(scratchPath + @"\output", container);

                Trace.WriteLine("Done uploading.");
            }
        }

        public override bool OnStart()
        {
            var controlContainer = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString")).CreateCloudBlobClient().GetContainerReference("control");
            if (controlContainer.CreateIfNotExist())
            {
                controlContainer.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            }
            // upload only if the XAP isn't already there (dev is responsible for uploading future versions)
            var xapBlob = controlContainer.GetBlobReference("NetflixPivotViewer.xap");
            // critical to set the right content type on XAPs
            xapBlob.Properties.ContentType = "application/x-silverlight-app";
            // cache it for two hours
            xapBlob.Properties.CacheControl = "max-age=7200";
            try
            {
                xapBlob.UploadFile(Environment.GetEnvironmentVariable("RoleRoot") + @"\approot\NetflixPivotViewer.xap", new BlobRequestOptions { AccessCondition = AccessCondition.IfNoneMatch("*") });
            }
            catch (StorageClientException ex)
            {
                if (ex.ErrorCode != StorageErrorCode.BlobAlreadyExists)
                {
                    throw;
                }
            }

            // Make sure we can make as many connections to storage as we want
            ServicePointManager.DefaultConnectionLimit = 1000;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.
            RoleEnvironment.Changing += RoleEnvironmentChanging;

            return base.OnStart();
        }

        private void RoleEnvironmentChanging(object sender, RoleEnvironmentChangingEventArgs e)
        {
            // If a configuration setting is changing
            if (e.Changes.Any(change => change is RoleEnvironmentConfigurationSettingChange))
            {
                // Set e.Cancel to true to restart this role instance
                e.Cancel = true;
            }
        }
    }
}