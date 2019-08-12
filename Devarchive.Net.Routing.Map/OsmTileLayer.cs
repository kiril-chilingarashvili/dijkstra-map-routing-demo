using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Devarchive.Net.Routing.Map
{
    public class OsmTileLayer : Microsoft.Maps.MapControl.WPF.MapTileLayer
    {
        public OsmTileLayer()
        {
            TileSource = new OsmTileSource();
        }

        public string UriFormat
        {
            get { return TileSource.UriFormat; }
            set { TileSource.UriFormat = value; }
        }
    }
}
