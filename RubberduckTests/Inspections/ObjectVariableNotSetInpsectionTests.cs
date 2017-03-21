using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rubberduck.Inspections;
using Rubberduck.Inspections.QuickFixes;
using Rubberduck.Parsing.VBA;
using Rubberduck.VBEditor.SafeComWrappers;
using Rubberduck.VBEditor.SafeComWrappers.Abstract;
using RubberduckTests.Mocks;

namespace RubberduckTests.Inspections
{
    [TestClass]
    public class ObjectVariableNotSetInpsectionTests
    {
        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_GivenIndexerObjectAccess_ReturnsNoResult()
        {
            const string inputCode = @"
Private Sub DoSomething()
    Dim target As Object
    Set target = CreateObject(""Scripting.Dictionary"")
    target(""foo"") = 42
End Sub
";
            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 0);
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_GivenStringVariable_ReturnsNoResult()
        {
            const string inputCode = @"
Private Sub Workbook_Open()
    
    Dim target As String
    target = Range(""A1"")
    
    target.Value = ""all good""

End Sub";

            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 0);
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_GivenVariantVariableAssignedObject_ReturnsResult()
        {

            const string inputCode = @"
Private Sub TestSub(ByRef testParam As Variant)
'whoCares is a LExprContext and is a known interesting declaration
    Dim target As Collection
    Set target = new Collection
    testParam = target             
    testParam.Add 100
End Sub";

            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 1);
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_GivenVariantVariableAssignedNewObject_ReturnsResult()
        {
            const string inputCode = @"
Private Sub TestSub(ByRef testParam As Variant)
'is a NewExprContext
    testParam = new Collection     
End Sub";

            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 1);
        }
        [TestMethod, Ignore]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_GivenVariantVariableAssignedRangeLiteral_ReturnsResult()
        {
            const string inputCode = @"
Private Sub TestSub(ByRef testParam As Variant)
'Range(""A1:C1"") is a LExprContext but is not a known interesting declaration
    testParam = Range(""A1:C1"")    
End Sub";

            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 1);
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_GivenVariantVariableAssignedDeclaredRange_ReturnsResult()
        {
            const string inputCode = @"
Private Sub TestSub(ByRef testParam As Variant, target As Range)
'target is a LExprContext and is a known interesting declaration
    testParam = target              
End Sub";

            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 1);
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_GivenVariantVariableAssignedDeclaredVariant_ReturnsNoResult()
        {
            const string inputCode = @"
Private Sub TestSub(ByRef testParam As Variant, target As Variant)
'target is a LExprContext, is a known interesting declaration - but is a Variant
    testParam = target           
End Sub";

            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 0);
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_GivenVariantVariableAssignedBaseType_ReturnsNoResult()
        {
            const string inputCode = @"
Private Sub Workbook_Open()
    Dim target As Variant
    target = ""A1""     'is a LiteralExprContext
End Sub";

            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 0);
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_GivenObjectVariableNotSet_ReturnsResult()
        {
            const string inputCode = @"
Private Sub Workbook_Open()
    
    Dim target As Range
    target = Range(""A1"")
    
    target.Value = ""forgot something?""

End Sub";

            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 1);
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_GivenObjectVariableNotSet_Ignored_DoesNotReturnResult()
        {
            const string inputCode = @"
Private Sub Workbook_Open()
    
    Dim target As Range
'@Ignore ObjectVariableNotSet
    target = Range(""A1"")
    
    target.Value = ""forgot something?""

End Sub";

            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 1);
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_GivenSetObjectVariable_ReturnsNoResult()
        {
            const string inputCode = @"
Private Sub Workbook_Open()
    
    Dim target As Range
    Set target = Range(""A1"")
    
    target.Value = ""All good""

End Sub";

            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 0);
        }

        //https://github.com/rubberduck-vba/Rubberduck/issues/2266
        [TestMethod]
        [DeploymentItem(@"Testfiles\")]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_FunctionReturnsArrayOfType_ReturnsNoResult()
        {
            const string inputCode = @"
Private Function GetSomeDictionaries() As Dictionary()
    Dim temp(0 To 1) As Worksheet
    Set temp(0) = New Dictionary
    Set temp(1) = New Dictionary
    GetSomeDictionaries = temp
End Function";

            var builder = new MockVbeBuilder();
            var project = builder.ProjectBuilder("VBAProject", ProjectProtection.Unprotected)
                .AddComponent("Codez", ComponentType.StandardModule, inputCode)
                .AddReference("Scripting", "", 1, 0, true)
                .Build();

            var vbe = builder.AddProject(project).Build();

            var parser = MockParser.Create(vbe.Object, new RubberduckParserState(vbe.Object));
            parser.State.AddTestLibrary("Scripting.1.0.xml");

            parser.Parse(new CancellationTokenSource());
            if (parser.State.Status >= ParserState.Error) { Assert.Inconclusive("Parser Error"); }

            var inspection = new ObjectVariableNotSetInspection(parser.State);
            var inspectionResults = inspection.GetInspectionResults();

            Assert.IsFalse(inspectionResults.Any());
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_IgnoreQuickFixWorks()
        {
            const string inputCode = @"
Private Sub Workbook_Open()
    
    Dim target As Range
    target = Range(""A1"")
    
    target.Value = ""forgot something?""

End Sub";

            const string expectedCode = @"
Private Sub Workbook_Open()
    
    Dim target As Range
'@Ignore ObjectVariableNotSet
    target = Range(""A1"")
    
    target.Value = ""forgot something?""

End Sub";

            IVBComponent component;
            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out component);
            var state = MockParser.CreateAndParse(vbe.Object);

            var inspection = new ObjectVariableNotSetInspection(state);
            var inspectionResults = inspection.GetInspectionResults();

            inspectionResults.First().QuickFixes.Single(s => s is IgnoreOnceQuickFix).Fix();

            Assert.AreEqual(expectedCode, component.CodeModule.Content());
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_ForFunctionAssignment_ReturnsResult()
        {
            const string inputCode = @"
Private Function CombineRanges(ByVal source As Range, ByVal toCombine As Range) As Range
    If source Is Nothing Then
        CombineRanges = toCombine 'no inspection result (but there should be one!)
    Else
        CombineRanges = Union(source, toCombine) 'no inspection result (but there should be one!)
    End If
End Function";

            const string expectedCode = @"
Private Function CombineRanges(ByVal source As Range, ByVal toCombine As Range) As Range
    If source Is Nothing Then
        Set CombineRanges = toCombine 'no inspection result (but there should be one!)
    Else
        Set CombineRanges = Union(source, toCombine) 'no inspection result (but there should be one!)
    End If
End Function";

            IVBComponent component;
            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out component);
            var state = MockParser.CreateAndParse(vbe.Object);

            var inspection = new ObjectVariableNotSetInspection(state);
            var inspectionResults = inspection.GetInspectionResults().ToList();

            Assert.AreEqual(2, inspectionResults.Count);
            foreach (var fix in inspectionResults.SelectMany(result => result.QuickFixes.Where(s => s is UseSetKeywordForObjectAssignmentQuickFix)))
            {
                fix.Fix();
            }
            Assert.AreEqual(expectedCode, component.CodeModule.Content());
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_ForPropertyGetAssignment_ReturnsResults()
        {
            const string inputCode = @"
Private example As MyObject
Public Property Get Example() As MyObject
    Example = example
End Property
";
            const string expectedCode = @"
Private example As MyObject
Public Property Get Example() As MyObject
    Set Example = example
End Property
";
            IVBComponent component;
            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out component);
            var state = MockParser.CreateAndParse(vbe.Object);

            var inspection = new ObjectVariableNotSetInspection(state);
            var inspectionResults = inspection.GetInspectionResults().ToList();

            Assert.AreEqual(1, inspectionResults.Count);
            foreach (var fix in inspectionResults.SelectMany(result => result.QuickFixes.Where(s => s is UseSetKeywordForObjectAssignmentQuickFix)))
            {
                fix.Fix();
            }
            Assert.AreEqual(expectedCode, component.CodeModule.Content());
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_LongPtrVariable_ReturnsNoResult()
        {
            const string inputCode = @"
Private Sub TestLongPtr()
    Dim handle as LongPtr
    handle = 123456
End Sub";

            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 0);
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_NoTypeSpecified_ReturnsResult()
        {
            const string inputCode = @"
Private Sub TestNonTyped(ByRef arg1)
    arg1 = new Collection
End Sub";
            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 1);
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_SelfAssigned_ReturnsNoResult()
        {
            const string inputCode = @"
Private Sub TestSelfAssigned()
    Dim arg1 As new Collection
    arg1.Add 7
End Sub";
            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 0);
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void ObjectVariableNotSet_EnumVariable_ReturnsNoResult()
        {

            const string inputCode = @"
Enum TestEnum
    EnumOne
    EnumTwo
    EnumThree
End Enum

Private Sub TestEnum()
    Dim enumVariable As TestEnum
    enumVariable = EnumThree
End Sub";

            AssertInputCodeYieldsExpectedInspectionResultCount(inputCode, 0);
        }

        private void AssertInputCodeYieldsExpectedInspectionResultCount(string inputCode, int expected)
        {
            IVBComponent component;
            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out component);
            var state = MockParser.CreateAndParse(vbe.Object);

            var inspection = new ObjectVariableNotSetInspection(state);
            var inspectionResults = inspection.GetInspectionResults();

            Assert.AreEqual(expected, inspectionResults.Count());
        }
    }
}
