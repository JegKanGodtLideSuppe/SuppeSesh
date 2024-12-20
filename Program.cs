using System.Data;
using System.Data.Sql;
using System.Data.SQLite;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Linq.Expressions;
using System.Net.NetworkInformation;

namespace SuppeSesh
{
    struct SqlParameter
    {
        internal string param;  // Parameter name for SQL command
        internal object value;  // Value associated with the parameter

        internal SqlParameter(string param, object value)
        {
            this.param = param;
            this.value = value;
        }
    }

    class Program
    {
        public static SQLiteConnection sqlite_conn;  // Global SQLite connection
        public static SQLiteCommand sqlite_cmd;      // Global SQLite command

        static void Main(string[] args)
        {
            StartDB();  // Initialize the database connection
            Loop();     // Start the main menu loop
        }

        public static void Loop()
        {
            switch (AskQuestion("Hvad vil du gøre?", new List<string>() { "Tilføje", "Slette", "Søge", "Afslut" })) // Asking the user what they would like to do
            {
                case 1:
                    Tilføj(); // Call function to add an item
                    break;
                case 2:
                    Slet(); // Call function to delete an item
                    break;
                case 3:
                    søge(); // Call function to search for items
                    break;
                case 4:
                    return; // Exit the loop and hence the program
            }
            Loop(); // Recursive call to continue the loop
        }
        

        static void StartDB()
        {
            sqlite_conn = new SQLiteConnection("data source=SuppeDB.db; Version = 3; New = false; Compress = true;"); // Setting up the SQLite connection
            sqlite_cmd = sqlite_conn.CreateCommand();  // Creating a command object
        }

        private static void Slet()
        {
            switch (AskQuestion("Hvad vil du slette?", new List<string>(){"Supper"})) // Asking what the user wants to delete
            {
                case 1:
                    string search = AskQuestion("hvad skal slettes?"); // Get user input for what to delete

                    // Attempt to parse the input as an integer ID
                    if (int.TryParse(search, out int value))
                    {
                        Suppe.DeleteSoupFromDb(value); // If successful, delete by ID
                        break;
                    }
                    
                    Suppe.DeleteSoupFromDb(search); // Otherwise, delete by name
                    break;
            }
        }
        
        private static void søge()
        {
            switch (AskQuestion("Hvad vil du søge efter?", new List<string>(){"Supper", "ingredienser"})) // Asking user what to search for
            {
                case 1:
                    List<Suppe> supper = new List<Suppe>();
                    // Searching for soups based on user input
                    foreach (int suppeid in Suppe.SøgSupper("%" + AskQuestion("Søgekriterie?") + "%")) 
                    {
                        supper.Add(new Suppe(suppeid)); // Adding found soups to the list
                    }
                    
                    // Displaying formatted soup information
                    Console.WriteLine("navn:         |Bouillon:  |Ingredienser:\n----------------------------------------");
                    foreach (Suppe suppe in supper)
                    {
                        Console.WriteLine("{0, -14}|{1, -11}|{2,13}", suppe.navn, suppe.bouillon.navn, suppe.ingredienser[0].navn);
                        foreach (Ingrediens ingrediens in suppe.ingredienser.Skip(1))
                        {
                            Console.WriteLine("                          |{0, 13}", ingrediens.navn);
                        }
                    }

                    Console.ReadLine();  // Wait for user input
                    
                    break;
                case 2:
                    break; // No operation defined for searching ingredients
            }
        }
        
        private static void Tilføj()
        {
            string navn; // Variable to hold the name of the added item
            switch (AskQuestion("Hvad vil du tilføje", new List<string>() { "Suppe", "Ingrediens" })) // Asking what the user wants to add
            {
                case 1:
                    List<Bouillon> bouillons = Bouillon.SearchBouillons(""); // Search for bouillons
                    Suppe suppe = new Suppe("", new List<Ingrediens>(), new Bouillon(1)) // Initialize new soup object
                    {
                        navn = AskQuestion("Hvad skal suppen hedde?"), // Get soup name from user
                        bouillon = bouillons[
                            AskQuestion("hvilken bouillon skal være i suppen?", bouillons.Select(i => i.navn).ToList()) - 1] // Choose bouillon
                    };

                    while (true)
                    {
                        navn = AskQuestion("Tilføj en ingrediens til suppen?"); // Ask for ingredient name
                        
                        suppe.ingredienser.Add(new Ingrediens(navn, true)); // Add new ingredient to the soup
                        
                        // Check if the ingredient already exists in the database
                        using (SQLiteDataReader data = Program.RunSqlCommand(
                                   "SELECT Ingredienser.Navn FROM Ingredienser WHERE Ingredienser.Navn like (@navn);",
                                   new List<SqlParameter>() { new SqlParameter("@navn", navn) }))
                        {
                            if (!data.HasRows)
                            {
                                Console.WriteLine("Ingrediensen findes ikke i databasen."); // Ingredient not found
                                // Ask if the user wants to add the ingredient to the database
                                if (YNQuestion("Vil du tilføje " + navn + " til databasen?"))
                                {
                                    Ingrediens.AddIngrediensToDb(new Ingrediens(navn,
                                        YNQuestion("Er " + navn + " vegansk?"))); // Prompt for vegan status
                                }
                            }
                            

                            // Check if the user wants to add another ingredient
                            if (!YNQuestion("Vil du tilføje endnu en ingrediens til suppen?"))
                            {
                                break; // Exit adding ingredients
                            }

                        }
                    }

                    suppe.AddSoupToDb(); // Save the soup to the database
                    break;
                case 2:
                    navn = AskQuestion("Tilføj en ingrediens til suppen?"); // Ask for ingredient name

                    // Check if the ingredient already exists in the database
                    using (SQLiteDataReader data = Program.RunSqlCommand(
                               "SELECT Ingredienser.Navn FROM Ingredienser WHERE Ingredienser.Navn like (@navn);",
                               new List<SqlParameter>() { new SqlParameter("@navn", navn) }))
                    {
                        if (data.HasRows)
                        {
                            Console.WriteLine("Ingrediensen findes allerede i databasen."); // Ingredient already exists
                            // Confirm with the user to add the existing ingredient
                            if (!YNQuestion("Er du sikke på du vil tilføje " + navn + " til databasen?"))
                            {
                                break; // Cancel operation
                            }
                        }
                        Ingrediens.AddIngrediensToDb(new Ingrediens(navn, YNQuestion("Er " + navn + " vegansk?"))); // Add ingredient with vegan status
                    } 
                    break;
            }
        }
    

        public static bool YNQuestion(string question)
        {
            Console.WriteLine(question + " Y/N"); // Prompt the user with Y/N question

            string input = Console.ReadKey().KeyChar.ToString(); // Read user input
            
            // Returning true for 'y' and false for 'n'
            if (input.ToLower() == "n")
            {
                return false;
            }
            
            if (input.ToLower() == "y")
            {
                return true;
            }
            
            Console.WriteLine("Ikke validt svar"); // Invalid response
            return YNQuestion(question); // Re-prompt if input is invalid
        }
        
        public static int AskQuestion(string question, List<string> answers)
        {
            Console.WriteLine("\n" + question); // Output the question

            for (int i = 0; i < answers.Count; i++)
            {
                Console.Write("[" + (i + 1) + "] "); // Print answer options
                Console.WriteLine(answers[i]);
            }

            ConsoleKeyInfo keyInfo = Console.ReadKey(); // Capture user input
            
            // Try to parse input to validate against the answer options
            if (int.TryParse(keyInfo.KeyChar.ToString(), out int num))
            {
                if (num >= 1 && num <= answers.Count)
                {
                    return num; // Valid input
                }
            }
            
            Console.WriteLine("Ikke valid input"); // Invalid input notification
            return AskQuestion(question, answers); // Re-prompt if invalid
        }

        public static string AskQuestion(string question)
        {
            Console.WriteLine("\n" + question); // Output the question
            
            string? input = Console.ReadLine(); // Get user input
            if (input == null)
            {
                Console.WriteLine("Ikke valid input"); // Invalid input notification
                return AskQuestion(question); // Re-prompt if null input
            }
            return input; // Return valid input
        }

        public static SQLiteDataReader RunSqlCommand(string sql, List<SqlParameter>? parameters = null)
        {
            // Closing the connection if it's already open
            if (sqlite_conn.State == ConnectionState.Open)
            {
                sqlite_conn.Close();
            }
            
            sqlite_cmd = sqlite_conn.CreateCommand(); // Creating the SQL command
            sqlite_cmd.CommandText = sql; // Setting SQL command text
            
            sqlite_cmd.Parameters.Clear(); // Clearing any previous parameters
            if (parameters == null) // If no parameters are provided
            {
                sqlite_conn.Open(); // Open the connection
                return sqlite_cmd.ExecuteReader(CommandBehavior.CloseConnection); // Execute command and return reader
            }
            
            // Adding parameters to the command
            foreach (SqlParameter parameter in parameters)
            {
                sqlite_cmd.Parameters.AddWithValue(parameter.param, parameter.value);
            }
            
            sqlite_conn.Open(); // Open the connection
            return sqlite_cmd.ExecuteReader(CommandBehavior.CloseConnection); // Execute command and return reader
        }
    }

    class Suppe
    {
        internal string navn; // Name of the soup
        internal List<Ingrediens> ingredienser = new List<Ingrediens>(); // List of ingredients
        internal Bouillon bouillon; // Bouillon used in the soup

        internal Suppe(string navn, List<Ingrediens> ingredienser, Bouillon bouillon)
        {
            this.navn = navn;
            this.ingredienser = ingredienser;
            this.bouillon = bouillon;
        }

        internal Suppe(int dbId)
        {
            List<int> ingredienserIds = new List<int>(); // List of ingredient IDs
            int bouillonId = 0; // Bouillon ID

            // Fetch soup data from the database
            using (SQLiteDataReader data = Program.RunSqlCommand(
                       "SELECT Supper.Navn, Ingredienser.IngrediensID, Supper.BouillonID FROM Supper\nJOIN SuppeIngredienser ON SuppeIngredienser.SuppeID = Supper.SuppeID\nJOIN Ingredienser ON Ingredienser.IngrediensID = SuppeIngredienser.IngrediensID\nWHERE Supper.SuppeID = @id;",
                       new List<SqlParameter>() { new SqlParameter("@id", dbId) }))
            {
                if (data.HasRows == false)
                {
                    Console.WriteLine("Ingen suppe fundet"); // No soup found
                    return; // Exit if no soup
                }

                while (data.Read())
                {
                    navn = data.GetString(0); // Assign soup name
                    ingredienserIds.Add(data.GetInt32(1)); // Collect ingredient IDs
                    bouillonId = data.GetInt32(2); // Assign bouillon ID
                }
            }

            foreach (int ingrediens in ingredienserIds)
            {
                ingredienser.Add(new Ingrediens(ingrediens)); // Add ingredients to the soup
            }
            
            bouillon = new Bouillon(bouillonId); // Initialize bouillon
        }

        internal static void DeleteSoupFromDb(int id)
        {
            // Deleting the soup from the database by ID
            Program.RunSqlCommand(
                "DELETE FROM SuppeIngredienser WHERE SuppeID = @id;\nDELETE FROM Supper WHERE SuppeID = @id;", 
                new List<SqlParameter>(){new SqlParameter("@id", id)});
        }

        internal static void DeleteSoupFromDb(string SøgeKriterie)
        {
            List<int> ids = SøgSupper(SøgeKriterie); // Search for soup IDs matching the criteria

            // Ask for confirmation before deleting multiple entries
            if (!Program.YNQuestion("er du sikke på du vil slette  " + ids.Count + " suppe(r) fra databasen?"))
            {
                return; // Exit if user is not sure
            }
            
            // Deleting all matching soups
            foreach (int id in ids)
            {
                DeleteSoupFromDb(id);
            }
        }
        
        internal void AddSoupToDb()
        {
            AddSoupToDb(this); // Call overloaded method to add current soup
        }
        
        internal static void AddSoupToDb(Suppe suppe)
        {
            // Inserting new soup into the database
            Program.RunSqlCommand("INSERT INTO Supper (Navn, BouillonID) VALUES(@navn, (SELECT BouillonID FROM Bouillon WHERE Bouillon.Navn = @bouillon))",
                new List<SqlParameter>()
                {
                    new SqlParameter("@navn", suppe.navn),
                    new SqlParameter("@bouillon", suppe.bouillon.navn)
                });
            // Adding ingredients for the soup
            foreach (Ingrediens ingrediens in suppe.ingredienser)
            {
                Program.RunSqlCommand("INSERT INTO SuppeIngredienser (SuppeID, IngrediensID) VALUES((SELECT SuppeID FROM Supper WHERE Supper.Navn = @suppe COLLATE NOCASE), \n(SELECT IngrediensID FROM Ingredienser WHERE Ingredienser.Navn = @ingrediens COLLATE NOCASE))",
                    new List<SqlParameter>()
                    {
                        new SqlParameter("@suppe", suppe.navn),
                        new SqlParameter("@ingrediens", ingrediens.navn)
                    });
            }
        }

        internal static List<int> SøgSupper(string søgeKriterie)
        {
            List<int> suppeIds = new List<int>(); // Initialize list of soup IDs

            // Search for soups with the given criteria
            using (SQLiteDataReader data = Program.RunSqlCommand("SELECT SuppeID FROM Supper WHERE Supper.Navn like (@søge) COLLATE NOCASE;", new List<SqlParameter>(){new SqlParameter("@søge", søgeKriterie)}))
            {
                if (!data.HasRows)
                {
                    return suppeIds; // Return empty list if no soups found
                }
                
                while (data.Read())
                {
                    suppeIds.Add(data.GetInt32(0)); // Collect soup IDs
                }
            }
            
            return suppeIds; // Return found soup IDs
        }
        
    }

    class Ingrediens
    {
        internal string navn; // Ingredient name
        internal bool vegansk; // Vegan status

        internal Ingrediens(string navn, bool vegansk)
        {
            this.navn = navn;
            this.vegansk = vegansk;
        }

        internal Ingrediens(int dbId)
        {
            // Fetching ingredient data from the database
            using (SQLiteDataReader data = Program.RunSqlCommand("SELECT Ingredienser.Navn, Ingredienser.Vegansk FROM Ingredienser WHERE IngrediensID = @id;",
                       new List<SqlParameter>() { new SqlParameter("@id", dbId) }))
            {
                if (data.HasRows == false)
                {
                    Console.WriteLine("Ingen ingrediens fundet"); // Ingredient not found
                    return; // Exit if no ingredient found
                }

                while (data.Read())
                {
                    navn = data.GetString(0); // Assign ingredient name
                    vegansk = Convert.ToBoolean(data.GetInt32(1)); // Assign vegan status
                }
            }
            
        }
        
        
        void AddIngrediensToDb()
        {
            AddIngrediensToDb(this); // Call overload to add current ingredient
        }

        internal static void AddIngrediensToDb(Ingrediens ingrediens)
        {
            // Inserting new ingredient into the database
            Program.RunSqlCommand("INSERT INTO Ingredienser (Navn, Vegansk)\nVALUES (@navn, @vegansk);",
                new List<SqlParameter>()
                {
                    new SqlParameter("@navn", ingrediens.navn),
                    new SqlParameter("@vegansk", Convert.ToInt32(ingrediens.vegansk))
                });
        }
        
        
    }

    class Menu
    {
        string navn; // Menu name
        Suppe suppe; // Associated soup
        Drink drink; // Associated drink

        Menu(string navn, Suppe suppe, Drink drink)
        {
            this.navn = navn;
            this.suppe = suppe;
            this.drink = drink;
        }

        private void AddMenuToDb()
        {
            AddMenuToDb(this); // Call overload to add current menu
        }
        
        internal void AddMenuToDb(Menu menu)
        {
            // Inserting new menu into the database
            Program.RunSqlCommand("INSERT INTO Menuer (Navn, SuppeID, DrinkID)\nVALUES(@navn, (SELECT SuppeID FROM Supper WHERE Supper.Navn = @suppe), (SELECT DrinkID FROM Drinks WHERE Drinks.Navn = @drink));",
                new List<SqlParameter>()
                {
                    new SqlParameter("@navn", menu.navn),
                    new SqlParameter("@suppe", menu.suppe.navn),
                    new SqlParameter("@drink", menu.drink.navn)
                });
            
        }
        
    }

    class Drink
    {
        internal string navn; // Drink name
        internal int mængde; // Quantity

        Drink(string navn, int mængde)
        {
            this.navn = navn;
            this.mængde = mængde;
        }

        Drink(int dbId)
        {
            // Fetching drink data from the database
            var data = Program.RunSqlCommand("SELECT Drinks.Navn, Drinks.Mængde FROM Drinks WHERE DrinkID = @id;",
                new List<SqlParameter>() { new SqlParameter("@id", dbId) });

            if (data.HasRows == false)
            {
                Console.WriteLine("Ingen drink fundet"); // Drink not found
                return; // Exit if no drink found
            }

            navn = data.GetString(0); // Assign drink name
            mængde = data.GetInt32(1); // Assign drink quantity
        }

        private void AddDrinkToDb()
        {
            AddDrinkToDb(this); // Call overload to add current drink
        }
        
        internal static void AddDrinkToDb(Drink drink)
        {
            // Inserting new drink into the database
            Program.RunSqlCommand("INSERT INTO Drinks (Navn, Mængde)\nVALUES(@navn, @mængde);",
                new List<SqlParameter>()
                {
                    new SqlParameter("@navn", drink.navn),
                    new SqlParameter("@mængde", drink.mængde)
                });
        }
    }

    class Bouillon
    {
        internal string navn; // Bouillon name

        Bouillon(string navn)
        {
            this.navn = navn;
        }

        internal Bouillon(int dbId)
        {
            // Fetching bouillon data from the database
            SQLiteDataReader data = Program.RunSqlCommand("SELECT Bouillon.Navn FROM Bouillon WHERE BouillonID = @id;",
                new List<SqlParameter>() { new SqlParameter("@id", dbId) });

            if (data.HasRows == false)
            {
                Console.WriteLine("Ingen bouillon fundet"); // Bouillon not found
                return; // Exit if no bouillon found
            }

            while (data.Read())
            {
                navn = data.GetString(0); // Assign bouillon name
            }
            data.Close();

        }

        private void AddBouillonToDb()
        {
            AddBouillonToDb(this); // Call overload to add current bouillon
        }
        
        internal static void AddBouillonToDb(Bouillon bouillon)
        {
            // Inserting new bouillon into the database
            Program.RunSqlCommand("INSERT INTO Bouillon (Navn)\nVALUES(@navn);",
                new List<SqlParameter>()
                {
                    new SqlParameter("@navn", bouillon.navn),
                });

        }

        internal static List<Bouillon> SearchBouillons(string søgeStreng)
        {
            List<Bouillon> bouillons = new List<Bouillon>(); // Initialize list of bouillons

            // Searching bouillons based on a search string
            using (SQLiteDataReader data = Program.RunSqlCommand("SELECT Bouillon.Navn FROM Bouillon WHERE Bouillon.Navn Like @search;", 
                       new List<SqlParameter>() {new SqlParameter("@search", "%"+søgeStreng+"%")}))
            {
                if (data.HasRows == false)
                {
                    Console.WriteLine("Ingen bouilloner der mathcer søgekriteriet"); // No matching bouillons found
                    return bouillons; // Return empty list
                }

                while (data.Read())
                {
                    bouillons.Add(new Bouillon(data.GetString(0))); // Collect bouillons found
                }
            }
            
            return bouillons; // Return list of found bouillons
        }
    }
}
