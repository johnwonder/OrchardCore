using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Resolvers;
using GraphQL.Types;
using OrchardCore.Apis.GraphQL.Queries;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents.Apis.GraphQL.Queries.Types;
using YesSql;

namespace OrchardCore.Contents.Apis.GraphQL.Queries.Providers
{
    public class ContentItemFieldTypeProvider : IDynamicQueryFieldTypeProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IContentManager _contentManager;
        private readonly IContentDefinitionManager _contentDefinitionManager;
        private readonly IEnumerable<ContentPart> _contentParts;
        private readonly IEnumerable<IObjectGraphType> _objectGraphTypes;
        private readonly ISession _session;

        public ContentItemFieldTypeProvider(
         IServiceProvider serviceProvider,
         IContentManager contentManager,
         IContentDefinitionManager contentDefinitionManager,
         IEnumerable<ContentPart> contentParts,
         IEnumerable<IObjectGraphType> objectGraphTypes,
         ISession session)
        {
            _serviceProvider = serviceProvider;
            _contentManager = contentManager;
            _contentDefinitionManager = contentDefinitionManager;
            _contentParts = contentParts;
            _objectGraphTypes = objectGraphTypes;
            _session = session;
        }

        public Task<IEnumerable<FieldType>> GetFields(ObjectGraphType state)
        {
            var fieldTypes = new List<FieldType>();

            var typeDefinitions = _contentDefinitionManager.ListTypeDefinitions();

            foreach (var typeDefinition in typeDefinitions)
            {
                var typeType = new ContentItemType
                {
                    Name = typeDefinition.Name // Blog
                };

                var queryArguments = new List<QueryArgument>();

                foreach (var part in typeDefinition.Parts)
                {
                    var name = part.Name; // About
                    var partName = part.PartDefinition.Name; // BagPart
                    
                    var contentPart = _contentParts.FirstOrDefault(x => x.GetType().Name == partName);

                    if (contentPart != null)
                    {
                        typeType.TryAddContentPart(part, contentPart);

                        // http://facebook.github.io/graphql/October2016/#sec-Input-Object-Values
                        var inputGraphType = new InputContentPartAutoRegisteringObjectGraphType(contentPart);
                        if (inputGraphType.Fields.Any())
                        {
                            queryArguments.Add(
                                new QueryArgument(
                                    inputGraphType.GetType()) {
                                    Name = name,
                                    ResolvedType = inputGraphType
                                });
                        }
                    }
                }

                var query = new ContentItemsQuery(_contentManager, _contentParts, _session)
                {
                    Name = typeDefinition.Name,
                    ResolvedType = new ListGraphType(typeType)
                };

                query.Arguments.AddRange(queryArguments);

                fieldTypes.Add(query);
            }

            return Task.FromResult<IEnumerable<FieldType>>(fieldTypes);
        }
    }
}
