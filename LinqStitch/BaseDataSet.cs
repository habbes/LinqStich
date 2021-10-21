using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace LinqStitch
{
    public class BaseDataSet : DataSet<object>
    {
        public BaseDataSet(DataContext context, IQueryable source) : base(context)
        {
            Expression = Expression.Constant(this);
            Source = source;
        }
        internal IQueryable Source { get; set; }
    }

    public class BaseDataSet<T> : DataSet<T>
    {
        public BaseDataSet(DataContext context, IQueryable source): base(context)
        {
            Expression = Expression.Constant(this);
            Source = source;
            IsBase = true;
        }
    }
}
