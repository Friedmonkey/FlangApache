using System.Collections.Generic;

namespace FlangWebsiteConsole
{
    public class AnalizerBase<Type>
    {
        public List<Type> Analizable = new List<Type>();
        public int Position = 0;

        public Type End { get; set; }
        public Type Current => Peek(0);

        //look forward
        public Type Peek(int off = 0)
        {
            if (Position + off >= Analizable.Count || Position + off < 0) return End;
            return Analizable[Position + off];
        }
        public AnalizerBase(Type end)
        {
            this.Analizable = new List<Type>();
            this.End = end;
        }
        public AnalizerBase(List<Type> txt, Type end)
        {
            this.Analizable = txt;
            this.End = end;
        }
    }
}
