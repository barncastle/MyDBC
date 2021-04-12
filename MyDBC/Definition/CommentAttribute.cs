using System;

namespace MyDBC.Definition
{
    public class CommentAttribute : Attribute
    {
        public readonly string Comment;

        public CommentAttribute(string comment) => Comment = comment;
    }
}
