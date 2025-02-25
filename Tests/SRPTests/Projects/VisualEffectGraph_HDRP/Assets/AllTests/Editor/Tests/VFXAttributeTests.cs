#if !UNITY_EDITOR_OSX || MAC_FORCE_TESTS
using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor.VFX.Block;
using UnityEditor.VFX.UI;
using UnityEngine;
using UnityEngine.VFX;

namespace UnityEditor.VFX.Test
{
    [TestFixture]
    public class VFXAttributeTests
    {
        private abstract class ContextTest : VFXContext
        {
            protected ContextTest(VFXContextType type) : base(type, VFXDataType.Particle, VFXDataType.Particle)
            {}

            public override bool CanBeCompiled()
            {
                return true;
            }

            public override IEnumerable<VFXAttributeInfo> attributes
            {
                get { return attributeInfos; }
            }

            public List<VFXAttributeInfo> attributeInfos = new List<VFXAttributeInfo>();
        }

        private class ContextTestInit : ContextTest
        {
            public ContextTestInit() : base(VFXContextType.Init) {}

            public override bool CanBeCompiled()
            {
                return true;
            }

        }
        private class ContextTestOutput : ContextTest
        {
            public ContextTestOutput() : base(VFXContextType.Output) {}

            public override bool CanBeCompiled()
            {
                return true;
            }

        }

        private VFXAttribute Attrib1 = new VFXAttribute("attrib1", VFXValueType.Float);
        private VFXAttribute Attrib2 = new VFXAttribute("attrib2", VFXValueType.Float2);
        private VFXAttribute Attrib3 = new VFXAttribute("attrib3", VFXValueType.Float3);

        [SetUp]
        public void Setup()
        {
            VFXViewWindow.GetAllWindows().ToList().ForEach(x => x.Close());
        }

        [TearDown]
        public void Cleanup()
        {
            VFXViewWindow.GetAllWindows().ToList().ForEach(x => x.Close());
            VFXTestCommon.DeleteAllTemporaryGraph();
        }

        private void TestAttributes(Action<VFXGraph> init, Action<VFXData> test)
        {
            var graph = ScriptableObject.CreateInstance<VFXGraph>();
            var initCtx = ScriptableObject.CreateInstance<ContextTestInit>();
            var outputCtx = ScriptableObject.CreateInstance<ContextTestOutput>();

            graph.AddChild(initCtx);
            graph.AddChild(outputCtx);
            initCtx.LinkTo(outputCtx);

            if (init != null)
                init(graph);

            var models = new HashSet<ScriptableObject>();
            graph.CollectDependencies(models);

            var data = models.OfType<VFXData>().First();
            data.CollectAttributes();

            test(data);
        }

        [Test]
        public void TestNoAttributes()
        {
            TestAttributes(
                null,
                (data) => Assert.AreEqual(0, data.GetNbAttributes()));
        }

        [Test]
        public void TestSimpleAttributes()
        {
            TestAttributes(
                (graph) =>
                {
                    ((ContextTest)graph[0]).attributeInfos.Add(new VFXAttributeInfo(Attrib1, VFXAttributeMode.Write));
                    ((ContextTest)graph[0]).attributeInfos.Add(new VFXAttributeInfo(Attrib2, VFXAttributeMode.ReadWrite));
                    ((ContextTest)graph[1]).attributeInfos.Add(new VFXAttributeInfo(Attrib1, VFXAttributeMode.Read));
                    ((ContextTest)graph[1]).attributeInfos.Add(new VFXAttributeInfo(Attrib3, VFXAttributeMode.Read));
                },
                (data) =>
                {
                    Assert.AreEqual(3, data.GetNbAttributes());
                    Assert.AreEqual(VFXAttributeMode.ReadWrite, data.GetAttributeMode(Attrib1));
                    Assert.AreEqual(VFXAttributeMode.ReadWrite, data.GetAttributeMode(Attrib2));
                    Assert.AreEqual(VFXAttributeMode.Read,      data.GetAttributeMode(Attrib3));
                });
        }

        private class BlockTest : VFXBlock
        {
            public override VFXContextType compatibleContexts { get { return VFXContextType.None; } }
            public override VFXDataType compatibleData { get { return VFXDataType.None; } }

            public override IEnumerable<VFXAttributeInfo> attributes
            {
                get
                {
                    yield return new VFXAttributeInfo(VFXAttribute.Age, VFXAttributeMode.Read);
                    yield return new VFXAttributeInfo(VFXAttribute.Age, VFXAttributeMode.Write);

                    yield return new VFXAttributeInfo(VFXAttribute.Lifetime, VFXAttributeMode.Read);
                    yield return new VFXAttributeInfo(VFXAttribute.Lifetime, VFXAttributeMode.Read);

                    yield return new VFXAttributeInfo(VFXAttribute.Mass, VFXAttributeMode.ReadWrite);
                }
            }
        }


        [Test]
        public void TestMergedAttributes()
        {
            var block = ScriptableObject.CreateInstance<BlockTest>();
            List<VFXAttributeInfo> blockAttributes = block.mergedAttributes.ToList();

            Assert.AreEqual(3, blockAttributes.Count);

            Assert.IsFalse(blockAttributes.Exists(a => a.Equals(new VFXAttributeInfo(VFXAttribute.Age, VFXAttributeMode.Read))));
            Assert.IsFalse(blockAttributes.Exists(a => a.Equals(new VFXAttributeInfo(VFXAttribute.Age, VFXAttributeMode.Write))));

            Assert.IsTrue(blockAttributes.Exists(a => a.Equals(new VFXAttributeInfo(VFXAttribute.Age, VFXAttributeMode.ReadWrite))));
            Assert.IsTrue(blockAttributes.Exists(a => a.Equals(new VFXAttributeInfo(VFXAttribute.Lifetime, VFXAttributeMode.Read))));
            Assert.IsTrue(blockAttributes.Exists(a => a.Equals(new VFXAttributeInfo(VFXAttribute.Mass, VFXAttributeMode.ReadWrite))));
        }

        [Test]
        public void SetAttribute_Default_Value_Is_Taken_Into_Account_Alpha()
        {
            var setAttribute = ScriptableObject.CreateInstance<Block.SetAttribute>();
            setAttribute.SetSettingValue("attribute", "alpha");

            var alphaReference = (float)VFXAttribute.Alpha.value.GetContent();
            Assert.AreNotEqual(0.0f, alphaReference);

            var alphaValue = (float)setAttribute.inputSlots[0].value;
            Assert.AreEqual(alphaReference, alphaValue);
        }

        [Test]
        public void SetAttribute_Default_Value_Is_Taken_Into_Account_Scale()
        {
            var setAttribute = ScriptableObject.CreateInstance<Block.SetAttribute>();
            setAttribute.SetSettingValue("attribute", "scale"); //variadic

            var scaleReference = new Vector3((float)VFXAttribute.ScaleX.value.GetContent(), (float)VFXAttribute.ScaleY.value.GetContent(), (float)VFXAttribute.ScaleZ.value.GetContent());
            Assert.AreNotEqual(0.0f, scaleReference.x);
            Assert.AreNotEqual(0.0f, scaleReference.y);
            Assert.AreNotEqual(0.0f, scaleReference.z);

            var scaleValue = (Vector3)setAttribute.inputSlots[0].value;
            Assert.AreEqual(scaleReference.x, scaleValue.x);
            Assert.AreEqual(scaleReference.y, scaleValue.y);
            Assert.AreEqual(scaleReference.z, scaleValue.z);
        }

        //Case 1166905
        [Test]
        public void SetAttribute_Inheriting_Source_Doesnt_Require_Seed()
        {
            var setAttribute = ScriptableObject.CreateInstance<Block.SetAttribute>();
            setAttribute.SetSettingValue("attribute", "alpha");
            setAttribute.SetSettingValue("Source", Block.SetAttribute.ValueSource.Source);
            setAttribute.SetSettingValue("Random", Block.RandomMode.Uniform);

            Assert.IsFalse(setAttribute.attributes.Any(o => o.attrib.name == VFXAttribute.Seed.name));
        }

        [TestCase("name with space", true)]
        [TestCase("1startWithNumber", true)]
        [TestCase("Special*Character", true)]
        [TestCase("ValidName", false)]
        public void SetCustomAttribute_Name_Validation(string name, bool shouldRaiseError)
        {
            CheckCustomAttributeName<SetCustomAttribute>(name, shouldRaiseError);
        }

        [TestCase("name with space", true)]
        [TestCase("1startWithNumber", true)]
        [TestCase("Special*Character", true)]
        [TestCase("ValidName", false)]
        public void GetCustomAttribute_Name_Validation(string name, bool shouldRaiseError)
        {
            CheckCustomAttributeName<GetCustomAttribute>(name, shouldRaiseError);
        }

        private void CheckCustomAttributeName<T>(string name, bool shouldRaiseError) where T : VFXModel
        {
            var graph = VFXTestCommon.MakeTemporaryGraph();
            var window = VFXViewWindow.GetWindow(graph, true);
            window.LoadResource(graph.visualEffectResource);
            VFXModel modelWithError = null;
            window.graphView.errorManager.onRegisterError += (model, origin, error, errorType, description) =>
            {
                Assert.IsNull(modelWithError, "The error seems to have been raise more than once");
                if (errorType == VFXErrorType.Error)
                {
                    modelWithError = model;
                }
            };


            var customAttributeNode = ScriptableObject.CreateInstance<T>();
            customAttributeNode.SetSettingValue("attribute", name);
            if (customAttributeNode is VFXOperator)
            {
                graph.AddChild(customAttributeNode);
            }
            else
            {
                var updateContext = ScriptableObject.CreateInstance<VFXBasicUpdate>();
                updateContext.AddChild(customAttributeNode);
                graph.AddChild(updateContext);
            }

            customAttributeNode.RefreshErrors();

            Assert.AreEqual(shouldRaiseError, customAttributeNode == modelWithError);
            if (shouldRaiseError)
            {
                Assert.AreEqual(customAttributeNode, modelWithError, $"A custom attribute with a name like '{name}' must raised an error feedback");
            }
            else
            {
                Assert.IsNull(modelWithError, $"A custom attribute name like '{name}' should not raise any error feedback");
            }
        }
    }
}
#endif
