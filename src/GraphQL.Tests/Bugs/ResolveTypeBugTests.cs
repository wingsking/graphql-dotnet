
using System;
using System.Linq;
using GraphQL.Http;
using GraphQL.StarWars.IoC;
using GraphQL.Types;
using GraphQL.Utilities;
using Shouldly;
using Xunit;

namespace GraphQL.Tests.Bugs
{
    public enum EDataType
    {
        String,
        Int
    }

    public class HtTag
    {
        public string Alias { get; set; }
        public EDataType DataType { get; set; }
        public object Value { get; set;}
    }

    public class IntegerTag : ObjectGraphType
    {
        public IntegerTag()
        {
            Field<IdGraphType>(
                "alias",
                "the unique alias of the tag"
            );
            Field<IntGraphType>("value");
            Interface<TagInterface>();
        }
    }

    public class StringTag : ObjectGraphType
    {
        public StringTag()
        {
            Field<IdGraphType>(
                "alias",
                "the unique alias of the tag"
            );
            Field<StringGraphType>("value");
            Interface<TagInterface>();
        }
    }

    public class TagInterface : InterfaceGraphType
    {
        public TagInterface(
            IntegerTag integerTag,
            StringTag stringTag)
        {
            Name = "Tag";
            Description = "A resource which points to a value";

            Field<IdGraphType>(
                "alias",
                "the unique alias of the tag"
            );

            ResolveType = obj =>
            {
                var types = this.PossibleTypes.ToList();

                if (obj is HtTag)
                {
                    switch ((obj as HtTag).DataType)
                    {
                        case EDataType.Int:
                            return integerTag;

                        default:
                            return stringTag;
                    }
                }

                throw new ArgumentOutOfRangeException($"Could not resolve graph type for {obj.GetType().Name}");
            };
        }
    }

    public class TagQuery : ObjectGraphType
    {
        public TagQuery()
        {
            Name = "Query";
            Field<ListGraphType<NonNullGraphType<TagInterface>>>("allTags", resolve: ctx =>
            {
                return ctx.RootValue;
            });
        }
    }

    public class TagSchema : Schema
    {
        public TagSchema(IDependencyResolver resolver) : base(resolver)
        {
            RegisterType<StringTag>();
            RegisterType<IntegerTag>();
            Query = resolver.Resolve<TagQuery>();
        }
    }

    public class ResolveTypeBugTests
    {
        [Fact]
        public void resolve_type_works()
        {
            var container = new SimpleContainer();
            container.Register<TagQuery>();
            container.Singleton<StringTag>();
            container.Singleton<IntegerTag>();
            container.Register<TagInterface>();
            container.Singleton<ISchema>(new TagSchema(new FuncDependencyResolver(type => container.Get(type))));

            var schema = container.Get<ISchema>();

            var jsonSchema = new SchemaPrinter(schema).Print();

            var result = schema.Execute(_=>
            {
                _.ExposeExceptions = true;
                _.Root = new []
                {
                     new HtTag { Alias = "one", DataType = EDataType.String, Value = "A Name" },
                     new HtTag { Alias = "two", DataType = EDataType.Int, Value = 123 }
                };
                _.Query = @"
                {
                    allTags {
                        __typename
                        alias
                        ... on StringTag {
                            value
                        }
                        ... on IntegerTag {
                            value
                        }
                    }
                }";
            });

            result.Replace(Environment.NewLine, "").ShouldBe("{  \"data\": {    \"allTags\": [      {        \"__typename\": \"StringTag\",        \"alias\": \"one\",        \"value\": \"A Name\"      },      {        \"__typename\": \"IntegerTag\",        \"alias\": \"two\",        \"value\": 123      }    ]  }}");
        }
    }
}
