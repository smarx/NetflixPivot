using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Services.Client;
using System.IO;
using System.Net;
using Microsoft.DeepZoomTools;
using System.Diagnostics;
using System.Xml.Linq;
using System.Xml;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using NetflixPivot_Worker.NetflixOData;
using System.Threading.Tasks;
using System.Threading;

namespace NetflixPivot_Worker
{
    public static class HexExtensions
    {
        public static string ToHex(this string input, Encoding encoding)
        {
            return string.Join("", encoding.GetBytes(input).Select(b => b.ToString("x2")).ToArray());
        }
        public static string ToHex(this string input)
        {
            return ToHex(input, Encoding.ASCII);
        }
    }

    class NetflixPivotCreator
    {
        private static void CreateCxml(string outputDirectory, IEnumerable<Title> titles, string suffix)
        {
            XNamespace ns = "http://schemas.microsoft.com/collection/metadata/2009";
            XNamespace p = "http://schemas.microsoft.com/livelabs/pivot/collection/2009";
            var items = new XElement(ns + "Items", new XAttribute("ImgBase", string.Format("collection-{0}.dzc", suffix)));
            int i = 0;
            foreach (var title in titles)
            {
                var item = new XElement(ns + "Item", new XAttribute("Img", string.Format("#{0}", i++)), new XAttribute("Id", title.Id), new XAttribute("Href", title.Url), new XAttribute("Name", title.Name));
                var facets = new XElement(ns + "Facets",
                    new XElement(ns + "Facet",
                        new XAttribute("Name", "MPAA Rating"),
                        new XElement(ns + "String",
                            new XAttribute("Value", title.Rating))),
                    new XElement(ns + "Facet",
                        new XAttribute("Name", "Rating"),
                        new XElement(ns + "Number",
                            new XAttribute("Value", title.AverageRating))),
                    new XElement(ns + "Facet",
                        new XAttribute("Name", "Year"),
                        new XElement(ns + "Number",
                            new XAttribute("Value", title.ReleaseYear))));
                if (title.Genres.Any())
                {
                    facets.Add(new XElement(ns + "Facet", new XAttribute("Name", "Genre"),
                        title.Genres.Select(g => new XElement(ns + "String", new XAttribute("Value", g.Name)))));
                }
                if (title.Cast.Any())
                {
                    facets.Add(new XElement(ns + "Facet", new XAttribute("Name", "Cast"),
                        title.Cast.Select(c => new XElement(ns + "String", new XAttribute("Value", c.Name)))));
                }
                if (title.Directors.Any())
                {
                    facets.Add(new XElement(ns + "Facet", new XAttribute("Name", "Director"),
                        title.Directors.Select(d => new XElement(ns + "String", new XAttribute("Value", d.Name)))));
                }
                // ignore absurd values (OData feed seemed to have some dates in the 3000s)
                if (title.Instant.AvailableFrom != null && title.Instant.AvailableFrom < (DateTime.UtcNow + TimeSpan.FromDays(36500)))
                {
                    facets.Add(new XElement(ns + "Facet", new XAttribute("Name", "AvailableFrom"),
                        new XElement(ns + "DateTime", new XAttribute("Value", title.Instant.AvailableFrom))));
                }
                // ignore absurd values (OData feed seemed to have some dates in the 3000s)
                if (title.Instant.AvailableTo != null && title.Instant.AvailableTo < (DateTime.UtcNow + TimeSpan.FromDays(36500)))
                {
                    facets.Add(new XElement(ns + "Facet", new XAttribute("Name", "AvailableTo"),
                        new XElement(ns + "DateTime", new XAttribute("Value", title.Instant.AvailableTo))));
                }
                item.Add(facets);
                items.Add(item);
            }
            var doc = new XDocument(
                new XElement(ns + "Collection",
                    new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                    new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
                    new XAttribute("Name", "Netflix Instant Watch Results"),
                    new XAttribute("SchemaVersion", "1.0"),
                    new XAttribute(XNamespace.Xmlns + "p", "http://schemas.microsoft.com/livelabs/pivot/collection/2009"),
                    new XAttribute(p + "Supplement", string.Format("collection_secondary-{0}.cxml", suffix)),
                    new XElement(ns + "FacetCategories",
                        new XElement(ns + "FacetCategory",
                            new XAttribute("Name", "Rating"),
                            new XAttribute("Type", "Number")),
                        new XElement(ns + "FacetCategory",
                            new XAttribute("Name", "Genre"),
                            new XAttribute("Type", "String")),
                        new XElement(ns + "FacetCategory",
                            new XAttribute("Name", "MPAA Rating"),
                            new XAttribute("Type", "String")),
                        new XElement(ns + "FacetCategory",
                            new XAttribute("Name", "Year"),
                            new XAttribute("Type", "Number"),
                            new XAttribute("Format", "####")),
                        new XElement(ns + "FacetCategory",
                            new XAttribute("Name", "Cast"),
                            new XAttribute("Type", "String")),
                        new XElement(ns + "FacetCategory",
                            new XAttribute("Name", "Director"),
                            new XAttribute("Type", "String")),
                        new XElement(ns + "FacetCategory",
                            new XAttribute("Name", "AvailableFrom"),
                            new XAttribute("Type", "DateTime")),
                        new XElement(ns + "FacetCategory",
                            new XAttribute("Name", "AvailableTo"),
                            new XAttribute("Type", "DateTime"))
                    ),
                    items
                )
            );
            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, new XmlWriterSettings() { Encoding = Encoding.Unicode }))
            {
                doc.WriteTo(writer);
            }
            File.WriteAllText(string.Format(@"{0}\output\collection-{1}.cxml", outputDirectory, suffix), sb.ToString(), Encoding.Unicode);

            var docSecondary = new XDocument(
                new XElement(ns + "Collection",
                    new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                    new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
                    new XAttribute("SchemaVersion", "1.0"),
                    new XElement(ns + "Items",
                        titles.Select(title =>
                            new XElement(ns + "Item", new XAttribute("Id", title.Id),
                                new XElement(ns + "Description", HttpUtility.HtmlDecode(Regex.Replace(title.Synopsis, "<[^>]*>", string.Empty))))
                        )
                    )));
            sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, new XmlWriterSettings() { Encoding = Encoding.Unicode }))
            {
                docSecondary.WriteTo(writer);
            }
            File.WriteAllText(string.Format(@"{0}\output\collection_secondary-{1}.cxml", outputDirectory, suffix), sb.ToString(), Encoding.Unicode);
        }

        public static IEnumerable<Title> GetTopInstantWatchTitles(int howMany)
        {
            var context = new NetflixCatalog(new Uri("http://odata.netflix.com/Catalog"));
            DataServiceQueryContinuation<Title> token = null;
            var response = ((from title in context.Titles where title.Instant.Available && title.Instant.AvailableFrom <= DateTime.UtcNow && title.Type == "Movie" orderby title.AverageRating descending select title) as DataServiceQuery<Title>)
                .Expand("Genres,Cast,Directors")
                .Execute() as QueryOperationResponse<Title>;
            int count = 0;
            var ids = new HashSet<string>();
            do
            {
                if (token != null)
                {
                    response = (QueryOperationResponse<Title>)context.Execute<Title>(new UriBuilder(token.NextLinkUri) { Port = 80 }.Uri);
                }
                foreach (var title in response)
                {
                    if (ids.Add(title.Id))
                    {
                        if (count < howMany)
                        {
                            yield return title;
                        }
                        count++;
                    }
                }
                token = response.GetContinuation();
            }
            while (token != null && count < howMany);
        }

        public static void CreatePivotCollection(string outputDirectory)
        {
            Trace.WriteLine("Starting to create pivot collection.");
            Directory.CreateDirectory(string.Format(@"{0}\images", outputDirectory));
            Directory.CreateDirectory(string.Format(@"{0}\output", outputDirectory));

            var titles = new List<Title>();
            Parallel.ForEach(GetTopInstantWatchTitles(3000), new ParallelOptions { MaxDegreeOfParallelism = 16 }, (title) =>
            {
                var client = new WebClient();
                // retry three times
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        var boxArtUrl = title.BoxArt.HighDefinitionUrl ?? title.BoxArt.LargeUrl ?? title.BoxArt.MediumUrl ?? title.BoxArt.SmallUrl;
                        if (boxArtUrl != null)
                        {
                            Trace.WriteLine(string.Format("Downloading '{0}'", title.Id));
                            var imagePath = string.Format(@"{0}\images\{1}.jpg", outputDirectory, title.Id.ToHex());
                            client.DownloadFile(boxArtUrl, imagePath);
                            Trace.WriteLine(string.Format("Processing '{0}'", title.Id));
                            new ImageCreator().Create(imagePath, string.Format(@"{0}\output\{1}.xml", outputDirectory, title.Id.ToHex()));
                            lock (titles) { titles.Add(title); }
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine("Caught exception " + ex.ToString());
                        if (i < 2)
                        {
                            Trace.WriteLine("Retrying after 5 seconds.");
                        }
                        else
                        {
                            Trace.WriteLine("Giving up.");
                        }
                        // sleep 5 seconds before trying again
                        Thread.Sleep(TimeSpan.FromSeconds(5));
                    }
                }
            });

            string suffix = (DateTime.MaxValue - DateTime.UtcNow).Ticks.ToString("d19");

            Trace.WriteLine("Creating deep zoom collection.");

            new CollectionCreator().Create(titles.Select(t => string.Format(@"{0}\output\{1}.xml", outputDirectory, t.Id.ToHex())).ToList(), string.Format(@"{0}\output\collection-{1}.dzc", outputDirectory, suffix));

            Trace.WriteLine("Generating CXML.");

            CreateCxml(outputDirectory, titles, suffix);

            Trace.WriteLine("Done generating pivot collection.");
        }
    }
}