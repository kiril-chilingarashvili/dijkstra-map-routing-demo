using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Devarchive.Net.Routing.Map
{
    public class OsmTileSource : Microsoft.Maps.MapControl.WPF.TileSource
    {
        public override Uri GetUri(int x, int y, int zoomLevel)
        {
            return new Uri(UriFormat.
                           Replace("{x}", x.ToString()).
                           Replace("{y}", y.ToString()).
                           Replace("{z}", zoomLevel.ToString()));
        }
    }
}
