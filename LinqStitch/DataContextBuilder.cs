using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LinqStitch
{
    public class DataContextBuilder
    {
        public DataContextBuilder(DataContext context)
        {
            Context = context;
        }

        internal Dictionary<string, object> DataSets = new();

        internal DataContext Context { get; }
        public DataSetBuilder<TDataSet> DataSet<TDataSet>()
        {
            var dataSetProperty = Context.GetType().GetProperties().First(p => TypeHelper.GetElementType(p.PropertyType) == typeof(TDataSet));
            if (DataSets.TryGetValue(dataSetProperty.Name, out object cachedBuilder))
            {
                return cachedBuilder as DataSetBuilder<TDataSet>;
            }

            DataSetBuilder<TDataSet> dataSetBuilder = new(this, dataSetProperty);
            DataSets[dataSetProperty.Name] = dataSetBuilder;
            return dataSetBuilder;
        }

        
    }

    public class DataSetBuilder<TDataSet>
    {
        IQueryable<TDataSet> _source;

        public DataSetBuilder(DataContextBuilder builder, PropertyInfo dataSetProperty)
        {
            ContextBuilder = builder;
            DataSetProperty = dataSetProperty;
            ElementType = typeof(TDataSet);
            PropertyConfigs = new();
        }

        public Dictionary<string, object> PropertyConfigs { get; private set; }

        internal DataContextBuilder ContextBuilder { get; }
        internal PropertyInfo DataSetProperty {get;set;}
        internal Type ElementType { get; set; }

        public IQueryable<TDataSet> Source { get => _source; }

        public DataSetBuilder<TDataSet> FromQueryable(IQueryable<TDataSet> source)
        {
            _source = source;
            return this;
        }

        public DataSetBuilder<TDataSet> FromQueryable(Func<IQueryable<TDataSet>> getSource)
        {
            _source = getSource();
            return this;
        }

        public DataSetBuilder<TDataSet> Property<TProp>(Expression<Func<TDataSet, TProp>> propertyFunc, Action<PropertyBuilder<TDataSet, TProp>> configure)
        {
            MemberExpression me = propertyFunc.Body as MemberExpression;
            if (me == null)
            {
                throw new Exception("Expression should be member-access expression");
            }

            

            PropertyInfo property = me.Member as PropertyInfo;

            PropertyBuilder<TDataSet, TProp> propBuilder;
            if (PropertyConfigs.TryGetValue(property.Name, out object cachedBuilder))
            {
                propBuilder = cachedBuilder as PropertyBuilder<TDataSet, TProp>;
            }
            else
            {
                propBuilder = new(this, property);
                PropertyConfigs[property.Name] = propBuilder;
            }

            configure(propBuilder);

            return this;
        }
    }

    public class PropertyBuilder<TDataSet, TProp>
    {
        public PropertyBuilder(DataSetBuilder<TDataSet> parentConfig, PropertyInfo property)
        {
            ParentConfig = parentConfig;


            Property = property;
        }

        internal DataSetBuilder<TDataSet> ParentConfig { get; }
        internal PropertyInfo Property { get; set; }

        private Func<PropertyFetchContext<TDataSet>, TProp> fetchFn;

        public PropertyBuilder<TDataSet, TProp> OnFetch(Func<PropertyFetchContext<TDataSet>, TProp> fetchFn)
        {
            this.fetchFn = fetchFn;
            return this;
        }

        public TProp FetchProperty(PropertyFetchContext<TDataSet> context)
        {
            return this.fetchFn(context);
        }
    }

    public class PropertyFetchContext<TDataSet>
    {
        public PropertyFetchContext(TDataSet element)
        {
            Element = element;
        }
        public TDataSet Element { get; }
    }


}
