using System;
using System.Collections.Generic;

namespace Unleash
{
    /// <inheritdoc />
    /// <summary>
    /// Unleash Feature Toggle Service
    /// </summary>
    public interface IUnleash : IDisposable
    {
        /// <summary>
        /// Gets a value indicating a feature is available or not.
        /// </summary>
        /// <param name="toggleName">The name of the toggle</param>
        bool IsEnabled(string toggleName);

        /// <summary>
        /// Gets a value indicating a feature is available or not.
        /// </summary>
        /// <param name="toggleName">The name of the toggle</param>
        /// <param name="defaultSetting">If a toggle is not found, default fallback setting will be returned. (default: false)</param>
        /// <returns></returns>
        bool IsEnabled(string toggleName, bool defaultSetting);

        /// <summary>
        /// Gets a value indicating a feature is available or not.
        /// </summary>
        /// <param name="toggleName">The name of the toggle</param>
        /// <param name="properties">
        /// Additional properties that are merged with the properties in the <see cref="UnleashContext"/>.
        /// These properties can be used in custom strategies and enable the strategy to act on runtime values.
        /// </param>
        /// <param name="defaultSetting">If a toggle is not found, default fallback setting will be returned. (default: false)</param>
        /// <returns></returns>
        bool IsEnabled(string toggleName, Dictionary<string, string> properties, bool defaultSetting = false);
    }
}