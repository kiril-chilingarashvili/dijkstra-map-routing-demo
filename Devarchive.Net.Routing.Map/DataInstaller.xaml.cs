using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Devarchive.Net.Routing.Map
{
    public partial class DataInstaller : Window, INotifyPropertyChanged
    {
        public DataInstaller()
        {
            InitializeComponent();

            DataContext = this;

            Loaded += DataInstaller_Loaded;
            Closing += DataInstaller_Closing;
        }

        #region IsLoading

        private bool mIsLoading = true;
        public bool IsLoading
        {
            get { return mIsLoading; }
            private set
            {
                if (mIsLoading != value)
                {
                    mIsLoading = value;
                    RaiseChanged(() => this.IsLoading);
                }
            }
        }

        #endregion

        #region Status

        private string mStatus = null;
        public string Status
        {
            get { return mStatus; }
            private set
            {
                if (mStatus != value)
                {
                    mStatus = value;
                    RaiseChanged(() => this.Status);
                }
            }
        }

        #endregion

        private void DataInstaller_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = IsLoading;
        }

        private void DataInstaller_Loaded(object sender, RoutedEventArgs e)
        {
            IsLoading = true;
            new Thread(InstallDB).Start();
        }

        private void InstallDB()
        {
            DBHelper.InsertDBData(UpdateStatus);
            UpdateStatus("Finished");
            Thread.Sleep(500);
            Dispatcher.BeginInvoke(new Action(Finished));
        }

        private void Finished()
        {
            IsLoading = false;
            Close();
        }

        private void UpdateStatus(string status)
        {
            Dispatcher.BeginInvoke(new Action(() => Status = status));
        }

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
