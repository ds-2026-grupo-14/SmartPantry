using SmartPantry.Products;
using Xunit;

namespace SmartPantry.EntityFrameworkCore.Applications;

[Collection(SmartPantryTestConsts.CollectionDefinitionName)]
public class EfCoreProductAppServiceTests : ProductAppServiceTests<SmartPantryEntityFrameworkCoreTestModule>
{

}

