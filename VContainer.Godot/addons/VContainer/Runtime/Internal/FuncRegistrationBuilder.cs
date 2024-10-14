using System;

namespace VContainer.Internal
{
	internal sealed class FuncRegistrationBuilder(Func<IObjectResolver, object> implementationProvider,
	    Type implementationType,
	    Lifetime lifetime)
	    : RegistrationBuilder(implementationType, lifetime)
    {
	    public override Registration Build()
        {
            var spawner = new FuncInstanceProvider(implementationProvider);
            return new Registration(ImplementationType, Lifetime, InterfaceTypes, spawner);
        }
    }
}
