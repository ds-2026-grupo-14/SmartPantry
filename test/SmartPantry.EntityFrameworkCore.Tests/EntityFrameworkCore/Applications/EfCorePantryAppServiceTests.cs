using SmartPantry.Pantry;
using Xunit;

namespace SmartPantry.EntityFrameworkCore.Applications;

[Collection(SmartPantryTestConsts.CollectionDefinitionName)]
public class EfCorePantryAppServiceTests : PantryAppServiceTests<SmartPantryEntityFrameworkCoreTestModule>
{

}
