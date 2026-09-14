using SmartPantry.Products;
using Xunit;

namespace SmartPantry.EntityFrameworkCore.Domains;

[Collection(SmartPantryTestConsts.CollectionDefinitionName)]
public class EfCoreProductDomainTests : ProductDomainTests<SmartPantryEntityFrameworkCoreTestModule>
{

}

