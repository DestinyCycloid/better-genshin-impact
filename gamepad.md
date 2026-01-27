dotnet test Test/BetterGenshinImpact.UnitTest/BetterGenshinImpact.UnitTest.csproj --filter "FullyQualifiedName~GamepadBindingsConfigTests|FullyQualifiedName~InputRouterTests|FullyQualifiedName~GamepadDebugWindowTests"

dotnet build BetterGenshinImpact.sln -c Debug
