using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Interop;
using System.ComponentModel;

namespace NetflixPivotViewer
{
    public partial class MainPage : UserControl
    {
        private string collectionUrl = null;
        private bool viewerStateNavigating = false;
        private bool itemNavigating = false;

        public MainPage(IDictionary<string, string> initParams)
        {
            InitializeComponent();
            collectionUrl = initParams["collectionUrl"];
            Viewer.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(Viewer_PropertyChanged);
            App.Current.Host.NavigationStateChanged += new EventHandler<System.Windows.Interop.NavigationStateChangedEventArgs>(Host_NavigationStateChanged);
            Host_NavigationStateChanged(null, new NavigationStateChangedEventArgs(null, App.Current.Host.NavigationState));
        }

        void Viewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ViewerState" && !string.IsNullOrEmpty(Viewer.ViewerState))
            {
                if (!viewerStateNavigating)
                {
                    App.Current.Host.NavigationState = string.Format("{0}/{1}", Viewer.ViewerState, Viewer.CurrentItemId);
                }
                else
                {
                    viewerStateNavigating = false;
                }
            }
            else if (e.PropertyName == "CurrentItemId")
            {
                if (!itemNavigating)
                {
                    App.Current.Host.NavigationState = string.Format("{0}/{1}", Viewer.ViewerState, Viewer.CurrentItemId);
                }
                else
                {
                    itemNavigating = false;
                }
            }
        }

        void Host_NavigationStateChanged(object sender, NavigationStateChangedEventArgs e)
        {
            string newViewerState, newItemId;
            if (e.NewNavigationState.Contains('/'))
            {
                var split = e.NewNavigationState.Split('/');
                newViewerState = split[0];
                newItemId = split[1];
                if (string.IsNullOrEmpty(newItemId))
                {
                    newItemId = null;
                }
            }
            else
            {
                newViewerState = e.NewNavigationState;
                newItemId = null;
            }
            if (newViewerState != Viewer.ViewerState)
            {
                viewerStateNavigating = true;
                Viewer.LoadCollection(collectionUrl, newViewerState);
            }
            if (newItemId != Viewer.CurrentItemId)
            {
                itemNavigating = true;
                Viewer.CurrentItemId = newItemId;
            }
        }
    }
}