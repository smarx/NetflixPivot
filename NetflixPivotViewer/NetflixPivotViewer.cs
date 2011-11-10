using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Pivot;
using System.Collections.Generic;
using System.Windows.Browser;

namespace NetflixPivotViewer
{
    public class NetflixPivotControl : PivotViewer
    {
        public NetflixPivotControl()
        {
            ItemActionExecuted += new EventHandler<ItemActionEventArgs>(NetflixPivotViewer_ItemActionExecuted);
            ItemDoubleClicked += new EventHandler<ItemEventArgs>(NetflixPivotViewer_ItemDoubleClicked);
        }

        private void BrowseTo(string itemId)
        {
            HtmlPage.Window.Navigate(new Uri(GetItem(itemId).Href));
        }

        private void NetflixPivotViewer_ItemDoubleClicked(object sender, ItemEventArgs e)
        {
            BrowseTo(e.ItemId);
        }

        private void NetflixPivotViewer_ItemActionExecuted(object sender, ItemActionEventArgs e)
        {
            BrowseTo(e.ItemId);
        }

        protected override List<CustomAction> GetCustomActionsForItem(string itemId)
        {
            var list = new List<CustomAction>();
            list.Add(new CustomAction("View on Netflix", null, "View this movie at Netflix", "view"));
            return list;
        }
    }
}