﻿using RebelCms.Framework.Persistence.Abstractions.Attribution.MetaData;
using RebelCms.Framework.Persistence.Model.Attribution;

namespace RebelCms.Framework.Persistence.Model.Constants.SerializationTypes
{
    using RebelCms.Framework.Data;

    public class DateTimeSerializationType : IAttributeSerializationDefinition
    {
        #region Implementation of IReferenceByName

        public string Alias { get; set; }
        public LocalizedString Name { get; set; }

        #endregion

        #region Implementation of IAttributeSerializationDefinition

        public DataSerializationTypes DataSerializationType { get { return DataSerializationTypes.Date; } }
        public dynamic Serialize(TypedAttribute value)
        {
            return (dynamic)value.DynamicValue;
        }

        #endregion
    }
}