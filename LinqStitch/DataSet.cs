using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace LinqStitch
{

    public abstract class DataSet : IEnumerable
    {
        public DataSet(DataContext context)
        {
            this.Context = context ?? throw new ArgumentNullException(nameof(context));
            IsBase = false;
        }

        public bool IsBase { get; protected set; }

        internal DataContext Context { get; set; }

        public IQueryable Source { get; protected set; }

        public Expression Expression { get; protected set; }

        public IQueryProvider Provider => Context;

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this.Context.Execute(this.Expression)).GetEnumerator();
        }
    }
    public abstract class DataSet<T> : DataSet, IQueryable, IQueryable<T>, IEnumerable<T>, IOrderedQueryable<T>
    {
        public DataSet(DataContext context): base(context) { }

        public Type ElementType => typeof(T);

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)this.Context.Execute(this.Expression)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this.Context.Execute(this.Expression)).GetEnumerator();
        }
    }


}
