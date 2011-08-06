namespace Paramore.Domain.Venues
{
    public class PostCode
    {
        private readonly string code;

        public PostCode(string code)
        {
            this.code = code;
        }

        public static implicit operator string(PostCode postCode)
        {
            return postCode.code;
        }
    }
}