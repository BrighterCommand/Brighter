using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    public class NoDispatchTimeExpression
    {
        
        private Expression _expression;

        public NoDispatchTimeExpression()
        {
            _expression = new Expression();
            _expression.ExpressionStatement = "Delivery = NULL";
        }


        public Expression Generate()
        {
            return _expression;
        }
    }
}
