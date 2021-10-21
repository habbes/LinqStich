using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace LinqStitch
{
    public class DerivedDataSet<T> : DataSet<T>
    {
        public DerivedDataSet(DataContext context, Expression expression): base(context)
        {
            this.Expression = expression;
        }
    }
}
