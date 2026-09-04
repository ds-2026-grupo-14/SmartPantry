using SmartPantry.Books;
using Xunit;

namespace SmartPantry.EntityFrameworkCore.Applications.Books;

[Collection(SmartPantryTestConsts.CollectionDefinitionName)]
public class EfCoreBookAppService_Tests : BookAppService_Tests<SmartPantryEntityFrameworkCoreTestModule>
{

}