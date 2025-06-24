namespace AssetStudioGUI;

using System.Windows.Forms;

using AssetStudio;

internal class GameObjectTreeNode : TreeNode
{
    public GameObject gameObject;

    public GameObjectTreeNode(GameObject gameObject)
    {
        this.gameObject = gameObject;
        Text = gameObject.m_Name;
    }
}