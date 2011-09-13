namespace Paramore.Domain.Venues
{
    public class PostCode
    {
        private readonly string code = string.Empty;

        public PostCode(string code)
        {
            this.code = code;
        }

        public PostCode() {}

        public static implicit operator string(PostCode postCode)
        {
            return postCode.code;
        }
    }
}