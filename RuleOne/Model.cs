
using Autodesk.Revit.DB;
using System.Collections;

namespace RuleOne
{
    public class Model : IEnumerable
    {
        public Model(Document  doc, Transform transform, bool isMep)
        {
            this.doc = doc;
            this.transform = transform;
            this.isMep = isMep;
        }

        public Document doc
        { get; set; }

        public Transform transform
        { get; set; }

        public bool isMep
        { get; set; }

        public IEnumerator GetEnumerator()
        {
            throw new System.NotImplementedException();
        }
    }
}
