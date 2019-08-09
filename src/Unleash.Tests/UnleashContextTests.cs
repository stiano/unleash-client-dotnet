using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;

namespace Unleash.Tests
{
    public class UnleashContextTests
    {
        [Test]
        public void Clone_FilledUnleashContext_ReturnsNewEquivalentUnleashContext()
        {
            var sut = new UnleashContext();

            var clonedContext = sut.Clone();

            clonedContext.Should().NotBe(sut);
            clonedContext.ShouldBeEquivalentTo(sut);
        }

        [Test]
        public void AppendProperties_UnleashContextPropertiesAreEmpty_AppendsPropertiesToUnleashContext()
        {
            var sut = new UnleashContext();
            var properties = new Dictionary<string, string>()
            {
                { "property1", "value1" },
                { "property2", "value2" }
            };

            sut.AppendProperties(properties);

            sut.Properties.Should().Equal(properties);
        }

        [Test]
        public void AppendProperties_PropertiesThatAreNotInTheUnleashContext_AppendsPropertiesToUnleashContext()
        {
            var property1 = new KeyValuePair<string, string>("property1", "value1");
            var property2 = new KeyValuePair<string, string>("property2", "value2");

            var sut = new UnleashContext
            {
                Properties = new Dictionary<string, string>
                {
                    { property1.Key, property1.Value }
                }
            };

            var properties = new Dictionary<string, string>()
            {
                { property2.Key, property2.Value }
            };

            sut.AppendProperties(properties);

            sut.Properties.Should().HaveCount(2);
            sut.Properties.Should().Contain(new [] { property1, property2 });
        }

        [Test]
        public void AppendProperties_PropertyExistsInUnleashContexts_OverwritesPropertyValue()
        {
            var sut = new UnleashContext
            {
                Properties = new Dictionary<string, string>
                {
                    { "property1", "value1" }
                }
            };

            var properties = new Dictionary<string, string>
            {
                { "property1", "New value1" }
            };

            sut.AppendProperties(properties);

            sut.Properties.Should().Equal(properties);
        }
    }
}