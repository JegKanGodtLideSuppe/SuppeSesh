namespace SuppeSesh
{
    class Pogram
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
    }

    class Suppe
    {
        List<Ingrediens> ingrediens = new List<Ingrediens>();
        Bullion bullion = new Bullion();
        
    }

    class Ingrediens
    {
        private string navn;
        private bool vegansk;
    }

    class Menu
    {
        Suppe suppe = new Suppe();
        Drink drink = new Drink();
    }

    class Drink
    {
        private string navn;
        private int mængde;
    }

    class Bouillon
    {
        private string navn;
    }
}

