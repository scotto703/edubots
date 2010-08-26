﻿//**************************************************************
// Class: BotLoad
// 
// Author: Joel McClain
//
// Refactored from: BakerIslandBots.cs By: Brian Schultz, Allan Blackford, 
//                                         Justin Hemker, Brian Wetherell 
// Date: 12-2-09
//
// Description: It all starts from this class.  It logs in bots,
//              loads the AIML, positions bots, loads callback 
//              methods, and processes all movement code.               
//
//**************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse.GUI;
using OpenMetaverse;
using System.Xml;
using System.IO;
using System.Reflection;
using AIMLbot;
using AIMLbot.Utils;
using System.Threading;
using System.Globalization;
using System.Timers;

namespace BotGUI
{
    class BotLoad
    {
        #region Virtual world properties
        string botName;
        public string Name
        {
            get
            {
                return botName;
            }
            set
            {
                botName = value;
            }
        }
        GridClient client = new GridClient();
        string FirstName; // Second Life login first name
        string LastName; // Second Life login last name
        string Password; // Second Life password
        string LocationName; //Second Life Province
        int x; // x - coordinate of bot location
        int y; // y - coordinate of bot location
        int z; // z - coordinate of bot location       
        #endregion

        #region AIMLbot properties
        string AimlPath; // this contains the path to AIML files. (AIML files are for dialogue)
        string SettingsPath; // this contains the path to the bot configuration file
        Bot myBot = new Bot();//This instantiates a new Bot.
        AIMLLoader Loader; //Function AIML loader loads AIML files associated with bot
        User myUser;
        List<User> people = new List<User>();

        System.Timers.Timer radiusTimer = new System.Timers.Timer();
        System.Timers.Timer ListTimer = new System.Timers.Timer();
        string chatQuestion;
        Request chatRequest;//This variable connected to Request.cs in the AIMLbot Project for chat requests
        Result chatResult;//This variable connected to Result.cs in the AIMLbot Project for response to chat requests
        string imQuestion;
        Request imRequest;//This variable connected to Request.cs in the AIMLbot Project for instant message requests
        Result imResult;//This variable connected to Result.cs in the AIMLbot Project for response to instant message requests
        Boolean movementExecuted;
        //Boolean CheckOut;
        BotEventReader eventReader;
        #endregion

        #region Constructor
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="name">name of bot to load</param>
        public BotLoad(string name)
        {
            botName = name;
            Loader = new AIMLLoader(myBot);
            myUser = new User(Name, myBot);
            eventReader = new BotEventReader(client, Name);
        }
        #endregion Constructor

        #region Methods
        public GridClient getClient()
        {
            return this.client;
        }
        private void ReadBotData()
        {
            string filePath = Environment.CurrentDirectory + "\\Bots\\" + botName + "\\BotConfig.xml";
            XmlTextReader reader = new XmlTextReader(new StreamReader(filePath));

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Name == "FirstName")
                        {
                            reader.Read();
                            FirstName = reader.Value;
                        }
                        else if (reader.Name == "LastName")
                        {
                            reader.Read();
                            LastName = reader.Value;
                        }
                        else if (reader.Name == "Password")
                        {
                            reader.Read();
                            Password = reader.Value;
                        }
                        else if (reader.Name == "Location")
                        {
                            reader.Read();
                            LocationName = reader.Value;
                        }
                        else if (reader.Name == "X")
                        {
                            reader.Read();
                            x = Int32.Parse(reader.Value);
                        }
                        else if (reader.Name == "Y")
                        {
                            reader.Read();
                            y = Int32.Parse(reader.Value);
                        }
                        else if (reader.Name == "Z")
                        {
                            reader.Read();
                            z = Int32.Parse(reader.Value);
                        }
                        else if (reader.Name == "AIMLPath")
                        {
                            reader.Read();
                            AimlPath = Environment.CurrentDirectory + reader.Value;
                        }
                        else if (reader.Name == "BotSettingsPath")
                        {
                            reader.Read();
                            SettingsPath = Environment.CurrentDirectory + reader.Value;
                        }
                        break;
                    case XmlNodeType.EndElement:
                        // All bot variables should be loaded, so close the file
                        if (reader.Name == "BotData")
                            reader.Close();
                        break;
                }
            }
        }
        public void LogBotOut()
        {
            client.Network.Logout();
        }
        public void InitBot()
        {
            // Get bots startup information
            ReadBotData();

            // create login parameters struct
            LoginParams clientLogin;

            // populate login srutct properties
            clientLogin = client.Network.DefaultLoginParams(FirstName, LastName, Password, "Baker Island Bots", Assembly.GetExecutingAssembly().GetName().Version.ToString());
            client.Settings.USE_LLSD_LOGIN = true;
            client.Settings.USE_ASSET_CACHE = true;
            string startLocation = NetworkManager.StartLocation(LocationName, x, y, z);
            clientLogin.Start = startLocation;

            // register events for this bot
            client.Network.SimDisconnected += new EventHandler<SimDisconnectedEventArgs>(Network_SimDisconnected);
            client.Network.LoginProgress +=new EventHandler<LoginProgressEventArgs>(Network_LoginProgress);
            client.Self.ChatFromSimulator += new EventHandler<ChatEventArgs>(Self_ChatFromSimulator);
            client.Self.ChatFromSimulator += new EventHandler<ChatEventArgs>(Log_ChatFromSimulator);
            client.Self.IM += new EventHandler<InstantMessageEventArgs>(Self_InstantMessage);
            radiusTimer.Elapsed += new ElapsedEventHandler(OnTimedEventRadius);
            ListTimer.Elapsed += new ElapsedEventHandler(OnTimedEventList);

            // Sets the timers
            radiusTimer.Interval = 7200000;
            radiusTimer.Enabled = true;
            ListTimer.Interval = 7200000;
            ListTimer.Enabled = true;

            // Load AIMLBot settings for this bot
            myBot.loadSettings(SettingsPath);
            myBot.isAcceptingUserInput = false;
            myBot.isAcceptingUserInput = true;

            // log bot in
            client.Network.BeginLogin(clientLogin);

            // Load AIML Files            
            botLoadAIML(AimlPath);
        }
        public void botLoadAIML(string path)
        {
            this.Loader.loadAIML(path);
        }
        private void PositionBot()
        {
            UUID managerChair = new UUID("62367cff-95e8-1c9d-73fe-f767848b9f5b");
            UUID receptionistChair = new UUID("0c5bd03d-9254-281b-ef48-0c0973b43a52");
            UUID loanofficerChair = new UUID("60d1155f-4186-468b-3238-64184a040b7d");
            UUID bankmanagerChair = new UUID("85c4b697-8cf2-43f7-2c24-d3ba10d4733f");
            Vector3 POSvector = new Vector3(96.7F, 180.0F, 26.3F);
            Vector3 tellercomputerVector = new Vector3(143.0F, 214.0F, 25.0F);

            if (client.Self.Name == "Alisandra Cascarino")
            {
                //start Alisandra's movement code
                //AlisandraMovement goAlisandra = new AlisandraMovement(client);
                BotEventReader alisEvents = new BotEventReader(client, botName);

                // Continous loop for Alisandra to keep wandering on the Island
                while (true)
                {
                    alisEvents.findEventInXmlFile(1);
                }
            }
            if (client.Self.Name == "Nimon Herbit")
            {
                //start Nimon's movement code
                //ShopperMovement goNimon = new ShopperMovement(client, 1);
                BotEventReader nimonEvents = new BotEventReader(client, botName);

                // Continous shopping loop for Nimon
                while (true)
                {
                    nimonEvents.findEventInXmlFile(1);
                }
            }
            if (client.Self.Name == "Britney Luminos")
            {
                client.Self.RequestSit(receptionistChair, Vector3.Zero);
                client.Self.Sit();
            }
            if (client.Self.Name == "Chesterfield Wrigglesworth")
            {
                client.Self.RequestSit(managerChair, Vector3.Zero);
                client.Self.Sit();
            }
            if (client.Self.Name == "Elminstyr Exonar")
            {
                client.Self.RequestSit(loanofficerChair, Vector3.Zero);
                client.Self.Sit();
            }
            if (client.Self.Name == "Franklin Fiertze")
            {
                client.Self.Movement.TurnToward(tellercomputerVector);
            }
            if (client.Self.Name == "Oriana Inglewood")
            {
                client.Self.Movement.TurnToward(POSvector);
            }
            if (client.Self.Name == "Tracy Helstein")
            {
                client.Self.RequestSit(bankmanagerChair, Vector3.Zero);
                client.Self.Sit();
            }
        }
        #endregion Methods

        #region Event Methods (CallBacks)

        void Self_ChatFromSimulator(object sender, ChatEventArgs e)
        {
            int radius = 7; //set the radius (in meters) avatars must be within to chat with bots
            Vector3 botPos = client.Self.SimPosition; //current location of bot
            Vector3 avatarPos = e.Position; //current location of avatar

            //Don't allow bots to respond to themselves, each other, or empty messages
            if ((e.SourceID != client.Self.AgentID) &&
                (e.FromName != "Alisandra Cascarino") &&
                (e.FromName != "Britney Luminos") &&
                (e.FromName != "Chesterfield Wrigglesworth") &&
                (e.FromName != "Elminstyr Exonar") &&
                (e.FromName != "Franklin Fiertze") &&
                (e.FromName != "Oriana Inglewood") &&
                (e.FromName != "Tracy Helstein") &&
                (e.FromName != "William Ormidale") &&
                (e.Message != ""))
            {
                //Don't allow bots to respond to avatars on different floors or beyond (radius) meters away
                //Must use SimPosition (RelativePosition returns position from object bot is sitting on)
                if (((client.Self.Name == "Alisandra Cascarino") && (e.Position.Z <= client.Self.SimPosition.Z + 1) ||
                    (client.Self.Name == "Britney Luminos") && (e.Position.Z <= client.Self.SimPosition.Z + 1) ||
                    (client.Self.Name == "Chesterfield Wrigglesworth") && (e.Position.Z >= client.Self.SimPosition.Z - 1) ||
                    (client.Self.Name == "Elminstyr Exonar") && (e.Position.Z >= client.Self.SimPosition.Z - 1) ||
                    (client.Self.Name == "Franklin Fiertze") && (e.Position.Z <= client.Self.SimPosition.Z + 1) ||
                    (client.Self.Name == "Oriana Inglewood") && (e.Position.Z <= client.Self.SimPosition.Z + 1) ||
                    (client.Self.Name == "William Ormidale") && (e.Position.Z <= client.Self.SimPosition.Z + 1) ||
                    (client.Self.Name == "Tracy Helstein") && (e.Position.Z <= client.Self.SimPosition.Z + 1) ||
                    (client.Self.Name == "Nimon Herbit") && (e.Position.Z <= client.Self.SimPosition.Z + 1)) &&
                    ((avatarPos != Vector3.Zero) && (Vector3.Distance(avatarPos, botPos) < radius)))
                {
                    //turn towards speaking avatar
                    client.Self.Movement.TurnToward(e.Position);

                    bool found = false;
                    int peopleIndex = 0;

                    //search people list for current user
                    for (int i = 0; i < people.Count; i++)
                    {
                        if (people[i].UserID == e.FromName)
                            found = true;
                    }

                    //if not found add current user to list
                    if (found == false)
                    {
                        User chatUser = new User(e.FromName, myBot);
                        people.Add(chatUser);

                    }

                    //get index of current user from list
                    for (int i = 0; i < people.Count; i++)
                    {
                        if (people[i].UserID == e.FromName)
                            peopleIndex = i;
                    }

                    //stores bot response from preceding occurance of Self_OnChat
                    //this is sent to BotMovement to test if movement should occur
                    if (chatResult != null)
                    {
                        chatQuestion = chatResult.RawOutput;
                    }

                    //send user message to AIMLbot.Request for encapsulation
                    chatRequest = new Request(e.Message, people[peopleIndex], myBot);

                    //send encapsulated user message for AIML response
                    chatResult = myBot.Chat(chatRequest);

                    //If user answers 'yes' (request) to a bot question (result), test to see if 
                    //it was a request to change location
                    if (chatRequest.rawInput.ToUpper() == "YES")
                    {
                        switch (client.Self.Name)
                        {
                            case "Oriana Inglewood":
                                movementExecuted = eventReader.findQuestionAndLoadEvent(chatQuestion);
                                break;
                        }

                    }

                    //Only display bot reply if previous user answer did not create botMovement
                    if (movementExecuted == false)
                    {
                        client.Self.AnimationStart(Animations.TYPE, false);
                        Thread.Sleep(3000);
                        client.Self.AnimationStop(Animations.TYPE, true);

                        //use this to not include avatar name in each reply
                        client.Self.Chat(chatResult.Output, 0, ChatType.Normal);
                    }

                    movementExecuted = false;
                }
            }
        }

        void Log_ChatFromSimulator(object sender, ChatEventArgs e)
        {
            string messageToWrite = e.Message;
            string bot = client.Self.Name;
            string avatarName = e.FromName;
            string dirName = "ChatLog";
            DateTime worldTime = DateTime.Now;
            string filePath = Environment.CurrentDirectory + "\\Bots\\" + botName + "\\" + dirName;

            DirectoryInfo dInfo = new DirectoryInfo(filePath);

            if (avatarName == client.Self.Name ||
                ((avatarName != "Alisandra Cascarino") &&
                 (avatarName != "Britney Luminos") &&
                 (avatarName != "Chesterfield Wrigglesworth") &&
                 (avatarName != "Elminstyr Exonar") &&
                 (avatarName != "Franklin Fiertze") &&
                 (avatarName != "Oriana Inglewood") &&
                 (avatarName != "Tracy Helstein") &&
                 (avatarName != "William Ormidale") &&
                 (avatarName != "Nimon Herbit")))
            {
                //Checks to see if the chat folder exists. If it doesn't, it is created.
                if (!dInfo.Exists)
                {
                    dInfo.Create();
                }

                if (messageToWrite != null && messageToWrite.Length != 0) //check for empty messages
                {
                    StreamWriter chat = new StreamWriter(filePath + "\\" + bot + ".txt", true);
                    chat.WriteLine(worldTime + ": " + avatarName + ": " + messageToWrite);
                    chat.Close();
                }
            }
        }

        void Network_LoginProgress(object sender, LoginProgressEventArgs e)
        {
            if (e.Status == LoginStatus.Success)
            {
                client.Self.Chat("Hi everyone!", 0, ChatType.Normal);
                PositionBot();
            }
        }

        void Self_InstantMessage(object sender, InstantMessageEventArgs e)
        {
            if (e.IM.Dialog == InstantMessageDialog.MessageFromAgent)
            {
                //turn towards speaking avatar
                client.Self.Movement.TurnToward(e.IM.Position);

                bool found = false;
                int peopleIndex = 0;

                //search people list for current user
                for (int i = 0; i < people.Count; i++)
                {
                    if (people[i].UserID == e.IM.FromAgentName.ToString())
                        found = true;
                }

                //if not found add current user to list
                if (found == false)
                {
                    User newIMuser = new User(e.IM.FromAgentName.ToString(), myBot);
                    people.Add(newIMuser);
                }

                //get index of current user from list
                for (int i = 0; i < people.Count; i++)
                {
                    if (people[i].UserID == e.IM.FromAgentName.ToString())
                        peopleIndex = i;
                }

                //stores bot response from preceding occurance of Self_OnChat
                //this is sent to BotMovement to test if movement should occur
                if (imResult != null)
                {
                    imQuestion = imResult.RawOutput;
                }

                //send user message to AIMLbot.Request for encapsulation
                imRequest = new Request(e.IM.Message, people[peopleIndex], myBot);

                //send encapsulated user message for AIML response
                imResult = myBot.Chat(imRequest);

                //If user answers 'yes' (request) to a bot question (result), test to see if 
                //it was a request to change location
                if (imRequest.rawInput.ToUpper() == "YES")
                {
                    switch (botName)
                    {
                        case "Oriana Inglewood":
                            OrianaMovement goOriana = new OrianaMovement(imQuestion, client);
                            movementExecuted = goOriana.getMovementExecuted();
                            break;
                        //add additional case statements here for other bots movement
                    }
                }

                //Only display bot reply if previous user answer did not create botMovement
                if (movementExecuted == false)
                {
                    client.Self.AnimationStart(Animations.TYPE, false);
                    Thread.Sleep(3000);
                    client.Self.AnimationStop(Animations.TYPE, true);
                    client.Self.InstantMessage(e.IM.FromAgentID, imResult.Output, e.IM.IMSessionID);
                }
            }

            movementExecuted = false;
        }

        void Network_SimDisconnected(object sender, SimDisconnectedEventArgs e)
        {
            if (e.Reason == NetworkManager.DisconnectType.NetworkTimeout)
            {
                System.Windows.Forms.MessageBox.Show(botName + "was disconnected. \n\n" +
                                                     "Reason: " + e.Reason.ToString() + "\n");// +
                                                     //"Message: " + message);
            }
        }

        void OnTimedEventList(object source, ElapsedEventArgs e)
        {
            people.Clear();
        }

        void OnTimedEventRadius(object source, ElapsedEventArgs e)
        {
            int radius = 10; //set the radius (in meters) to scan
            Vector3 mypos = client.Self.RelativePosition;

            //populate list with object in the radius
            List<Avatar> avatars = client.Network.CurrentSim.ObjectsAvatars.FindAll(delegate(Avatar avatar)
            {
                Vector3 pos = avatar.Position;
                Vector3 location = mypos;
                return ((avatar.ParentID == 0) && (pos != Vector3.Zero) && (Vector3.Distance(pos, location) < radius));
            }
               );
            bool found = false;
            for (int i = 0; i < avatars.Count; i++)
            {
                for (int j = 0; j < people.Count; j++)
                {
                    if (people[j].UserID == avatars[i].Name)
                    {
                        found = true;
                    }
                }

                if ((found == false) &&
                    (avatars[i].Name != client.Self.Name) &&
                    (avatars[i].Name != "Alisandra Cascarino") &&
                    (avatars[i].Name != "Britney Luminos") &&
                    (avatars[i].Name != "Chesterfield Wrigglesworth") &&
                    (avatars[i].Name != "Elminstyr Exonar") &&
                    (avatars[i].Name != "Franklin Fiertze") &&
                    (avatars[i].Name != "Oriana Inglewood") &&
                    (avatars[i].Name != "Tracy Hydefeld"))
                {
                    client.Self.AnimationStart(Animations.TYPE, false);
                    Thread.Sleep(3000);
                    client.Self.AnimationStop(Animations.TYPE, true);
                    client.Self.Chat("Hello would you like to chat " + avatars[i].Name, 0, ChatType.Normal);
                }
            }
        }

        #endregion Events
    }
}