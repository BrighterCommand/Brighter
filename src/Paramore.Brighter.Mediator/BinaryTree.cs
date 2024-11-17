namespace Paramore.Brighter.Mediator;

/// <summary>
/// Represents a node in a binary tree.
/// </summary>
/// <typeparam name="T">The type of the value stored in the node.</typeparam>
public class BinaryTree<T>
{
    /// <summary>
    /// Gets or sets the value of the node.
    /// </summary>
    public T Value { get; set; }

    /// <summary>
    /// Gets or sets the left child of the node.
    /// </summary>
    public BinaryTree<T>? Left { get; set; }

    /// <summary>
    /// Gets or sets the right child of the node.
    /// </summary>
    public BinaryTree<T>? Right { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryTree{T}"/> class.
    /// </summary>
    /// <param name="value">The value of the node.</param>
    public BinaryTree(T value)
    {
        Value = value;
    }

    /// <summary>
    /// Adds a left child to the node.
    /// </summary>
    /// <param name="value">The value of the left child.</param>
    /// <returns>The left child node.</returns>
    public BinaryTree<T> AddLeft(T value)
    {
        Left = new BinaryTree<T>(value);
        return Left;
    }

    /// <summary>
    /// Adds a right child to the node.
    /// </summary>
    /// <param name="value">The value of the right child.</param>
    /// <returns>The right child node.</returns>
    public BinaryTree<T> AddRight(T value)
    {
        Right = new BinaryTree<T>(value);
        return Right;
    }
}
