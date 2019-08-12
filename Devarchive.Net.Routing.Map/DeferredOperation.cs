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
using System.ComponentModel;
using System.Windows.Threading;

namespace Devarchive.Net.Routing.Map
{
    public class DeferredOperation : INotifyPropertyChanged
    {
        #region Class level variables

        private DispatcherTimer mTimer = new DispatcherTimer();
        private Action mAction = null;

        #endregion

        #region Constructor

        public DeferredOperation()
            : this(new TimeSpan(0, 0, 0, 0, 100))
        {
        }

        public DeferredOperation(TimeSpan interval)
        {
            mTimer.Interval = interval;
            mTimer.Stop();
            mTimer.Tick += new EventHandler(mTimer_Tick);
        }

        #endregion

        #region Properties

        private bool mIsBusy = false;
        public bool IsBusy
        {
            get { return mIsBusy; }
            set
            {
                mIsBusy = value;
                RaiseChanged(() => this.IsBusy);
            }
        }

        #endregion

        #region Methods

        public void BeginInvoke(Action action)
        {
            mTimer.Stop();
            IsBusy = true;
            mAction = action;
            mTimer.Start();
        }

        #endregion

        #region Event handlers

        private void mTimer_Tick(object sender, EventArgs e)
        {
            mTimer.Stop();
            if (mAction != null)
            {
                mAction();
            }
            IsBusy = false;
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaiseChanged(System.Linq.Expressions.Expression<System.Func<object>> propertyNameLambda)
        {
            var memberExpression = propertyNameLambda.Body as System.Linq.Expressions.MemberExpression;
            if (memberExpression == null)
            {
                var unaryExpression = (propertyNameLambda.Body as System.Linq.Expressions.UnaryExpression);
                memberExpression = unaryExpression.Operand as System.Linq.Expressions.MemberExpression;
            }
            var propertyName = memberExpression.Member.Name;
            if (!string.IsNullOrEmpty(propertyName))
            {
                var handler = PropertyChanged;
                if (handler != null)
                {
                    var e = new PropertyChangedEventArgs(propertyName);
                    handler(this, e);
                }
            }
        }

        #endregion
    }
}
