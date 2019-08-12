using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Maps.MapControl.WPF;

namespace Devarchive.Net.Routing.Map
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private DeferredOperation mOperation = new DeferredOperation();

        public MainWindow()
        {
            InitializeComponent();



            DataContext = this;
            Loaded += MainWindow_Loaded;
            map.MouseDoubleClick += map_MouseDoubleClick;
            map.MouseRightButtonDown += map_MouseRightButtonDown;
            map.MouseMove += map_MouseMove;
            
        }

        #region Point1

        private Location mPoint1 = null;
        public Location Point1
        {
            get { return mPoint1; }
            private set
            {
                if (mPoint1 != value)
                {
                    mPoint1 = value;
                    RaiseChanged(() => this.Point1);
                    Point1Updated();
                }
            }
        }

        private void Point1Updated()
        {
            Point1Text = Point1 != null ? Point1.ToString() : null;
        }

        #endregion

        #region Point1Text

        private string mPoint1Text = null;
        public string Point1Text
        {
            get { return mPoint1Text; }
            private set
            {
                if (mPoint1Text != value)
                {
                    mPoint1Text = value;
                    RaiseChanged(() => this.Point1Text);
                }
            }
        }

        #endregion

        #region Point2

        private Location mPoint2 = null;
        public Location Point2
        {
            get { return mPoint2; }
            private set
            {
                if (mPoint2 != value)
                {
                    mPoint2 = value;
                    RaiseChanged(() => this.Point2);
                    Point2Updated();
                }
            }
        }

        private void Point2Updated()
        {
            Point2Text = Point2 != null ? Point2.ToString() : null;
        }

        #endregion

        #region Point2Text

        private string mPoint2Text = null;
        public string Point2Text
        {
            get { return mPoint2Text; }
            private set
            {
                if (mPoint2Text != value)
                {
                    mPoint2Text = value;
                    RaiseChanged(() => this.Point2Text);
                }
            }
        }

        #endregion

        #region Route

        private MapPolyline mRoute = null;
        public MapPolyline Route
        {
            get { return mRoute; }
            private set
            {
                if (mRoute != value)
                {
                    if (mRoute != null)
                    {
                        DetachRoute();
                    }
                    mRoute = value;
                    if (mRoute != null)
                    {
                        AttachRoute();
                    }
                }
            }
        }

        private void AttachRoute()
        {
            map.Children.Add(Route);
        }

        private void DetachRoute()
        {
            map.Children.Remove(Route);
        }

        #endregion

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!DBHelper.HasData())
            {
                new DataInstaller().ShowDialog();
            }

            // pre-cache graph in CLR SP
            ShowRoute(41.71341, 44.77932, 41.71341, 44.77932);

            GoToTbilisi();
        }

        private void map_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Point1 = map.ViewportPointToLocation(e.GetPosition(map));
            ExecuteRoute(null);
        }

        private void map_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed)
            {
                mOperation.BeginInvoke(() =>
                    {
                        Point2 = map.ViewportPointToLocation(e.GetPosition(map));
                        ExecuteRoute(null);
                    });
            }
        }

        private void map_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point2 = map.ViewportPointToLocation(e.GetPosition(map));
            ExecuteRoute(null);
        }

        private void GoToTbilisi()
        {
            var lat = 41.71341;
            var lon = 44.77932;
            var zoomLevel = 13;
            var center = new Location(lat, lon);
            map.SetView(center, zoomLevel);
        }

        public void ShowRoute()
        {
            if (Point1 != null && Point2 != null)
            {
                ShowLocations(new List<Location>()
                {
                    Point1, Point2
                });
            }
        }

        public void ShowLocations(List<Location> locations)
        {
            if (locations.Count > 0)
            {
                if (locations.Count > 0)
                {
                    var bounds = new LocationRect(locations);
                    map.SetView(bounds);
                }
            }
        }

        #region RouteCommand

        private RelayCommand mRouteCommand;
        public RelayCommand RouteCommand
        {
            get
            {
                if (mRouteCommand == null)
                {
                    mRouteCommand = new RelayCommand(ExecuteRoute, CanExecuteRoute);
                }
                return mRouteCommand;
            }
        }

        private bool CanExecuteRoute(object param)
        {
            return
                Point1 != null &&
                Point2 != null;
        }

        private void ExecuteRoute(object param)
        {
            if (CanExecuteRoute(param))
            {
                var x1 = Point1.Latitude;
                var y1 = Point1.Longitude;
                var x2 = Point2.Latitude;
                var y2 = Point2.Longitude;

                ShowRoute(x1, y1, x2, y2);
            }
        }

        private void ShowRoute(double x1, double y1, double x2, double y2)
        {
            var data = DBHelper.GetRouteData(x1, y1, x2, y2);
            var line = new MapPolyline
            {
                Stroke = Brushes.Red,
                StrokeThickness = 3,
                ToolTip = data.Distance.ToString()

            };
            var coll = new LocationCollection();
            data.Coordinates
                .OrderBy(c => c.Index)
                .ToList()
                .ForEach(c => coll.Add(new Location(c.X, c.Y)));
            line.Locations = coll;
            Route = line;
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged = (sender, args) => { };

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }

        protected void RaiseChanged<TProperty>(System.Linq.Expressions.Expression<Func<TProperty>> propertyExpresion)
        {
            var property = propertyExpresion.Body as System.Linq.Expressions.MemberExpression;
            if (property == null || !(property.Member is System.Reflection.PropertyInfo) ||
                !IsPropertyOfThis(property))
            {
                throw new ArgumentException(string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    "Expression must be of the form 'this.PropertyName'. Invalid expression '{0}'.",
                    propertyExpresion), "propertyExpression");
            }

            this.OnPropertyChanged(property.Member.Name);
        }

        private bool IsPropertyOfThis(System.Linq.Expressions.MemberExpression property)
        {
            var constant = RemoveCast(property.Expression) as System.Linq.Expressions.ConstantExpression;
            return constant != null && constant.Value == this;
        }

        private System.Linq.Expressions.Expression RemoveCast(System.Linq.Expressions.Expression expression)
        {
            if (expression.NodeType == System.Linq.Expressions.ExpressionType.Convert ||
                expression.NodeType == System.Linq.Expressions.ExpressionType.ConvertChecked)
                return ((System.Linq.Expressions.UnaryExpression)expression).Operand;

            return expression;
        }

        #endregion
    }
}
