using System;
using Rebel.Framework.DependencyManagement;

namespace Rebel.Framework
{
    /// <summary>
    /// Used to identify that a type demands dependencies
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class DemandsDependenciesAttribute : Attribute
    {
        /// <summary>
        /// Used to identify that a class requires an <see cref="IDependencyDemandBuilder"/> to be invoked when it is instantiated from configuration
        /// </summary>
        /// <param name="demandBuilderType">Type of the demand builder.</param>
        public DemandsDependenciesAttribute(Type demandBuilderType)
        {
            DemandBuilderType = demandBuilderType;
        }

        public Type DemandBuilderType { get; protected set; }
    }
}