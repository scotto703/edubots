﻿//**************************************************************
// Class: BotEventReader
//
// Author: Joel McClain
//
// Date: 10-20-09
//
// Description: Processes events in the events.xml file for each
//              bot.  Calls BotAction for all actions, BotMove for 
//              all movements and BotChat for all conversation.
//***************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using OpenMetaverse;
using System.Threading;

namespace BotGUI
{
    class BotEventReader
    {
        #region Attributes
        /// <summary>
        /// Client of the bot to manipulate
        /// </summary>
        private GridClient client;
        /// <summary>
        /// Handles the movement events of the bot
        /// </summary>
        private BotMove botMovement;
        /// <summary>
        /// Handles actions of the bot
        /// </summary>
        private BotAction action;
        /// <summary>
        /// Handles chatting from the bot
        /// </summary>
        private BotChat chat;
        /// <summary>
        /// Path to the events.xml
        /// </summary>
        private string eventFilePath;
        /// <summary>
        /// Path to the questions.xml
        /// </summary>
        private string questionsPath;
        /// <summary>
        /// List of question and event pairs
        /// </summary>
        private List<questionsAndEvents> movementQuestions = new List<questionsAndEvents>();
        /// <summary>
        /// Construct that pairs a question and an event
        /// </summary>
        private struct questionsAndEvents
        {
            public string question;
            public int eventID;
        }
        /// <summary>
        /// Will pause a loop in the event reader when set to true
        /// Currently does nothing
        /// </summary>
        private bool eventPaused = false;
        /// <summary>
        /// Will exit most loops in the event reader when set to true
        /// </summary>
        private bool eventExited = false;
        /// <summary>
        /// True to pause the event managed by this reader
        /// false to resume an event
        /// Currently pauses only movements
        /// </summary>
        public bool pauseEvent
        {
            get
            {
                return eventPaused;
            }
            set
            {
                botMovement.pauseMove = value;
                eventPaused = value;
            }
        }
        /// <summary>
        /// True to exit and ignore events managed by this reader
        /// false to allow the reader to resume handling events
        /// Event reader may not immediately recognize the exit flag
        /// Processing a current interation continues. The next interation is blocked
        /// </summary>
        public bool exitEvent
        {
            get
            {
                return eventExited;
            }
            set
            {
                botMovement.exitMove = value;
                eventExited = value;
            }
        }   

        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="client">GridClient that is the client of the bot</param>
        /// <param name="name">String that is the name of the bot</param>
        public BotEventReader(GridClient client, string name)
        {
            this.client = client; 
            botMovement = new BotMove(client);
            action = new BotAction(client);
            chat = new BotChat(client);
            eventFilePath = Environment.CurrentDirectory + "\\Bots\\" + name + "\\Events\\events.xml";
            questionsPath = Environment.CurrentDirectory + "\\Bots\\" + name + "\\Events\\questions.xml";                       
            loadQuestions();
        }
        #endregion       

        #region Methods
        /// <summary>
        /// Loads all current movement questions into movementQuestions list
        /// </summary>
        private void loadQuestions()
        {
            // Open the questions.xml file
            XmlTextReader QReader = new XmlTextReader(new StreamReader(questionsPath));

            try
            {
                bool finished = false;

                // process the data in the file
                while (QReader.Read() && !finished && !eventExited)
                {
                    switch (QReader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (QReader.Name == "Question")
                            {
                                // create a temporary structure to load the data into
                                questionsAndEvents tempStruct = new questionsAndEvents();

                                // Read in the eventID attribute
                                while (QReader.MoveToNextAttribute())
                                {
                                    // read the name and discard it
                                    string name = QReader.Name;

                                    // read the event number for this question (needed to launch event when question is asked)
                                    tempStruct.eventID = Int32.Parse(QReader.Value);
                                }

                                // read the question and store it in the struct
                                QReader.Read();
                                tempStruct.question = QReader.Value;

                                // add the structure to the list of available questions
                                movementQuestions.Add(tempStruct);
                            }
                            break;
                        case XmlNodeType.EndElement:
                            // end of file
                            if (QReader.Name == "movementQuestions")
                                finished = true;
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                System.Windows.Forms.MessageBox.Show("Error reading questions.xml file: \n\n" + e.ToString());
            }
            finally
            {
                QReader.Close();
            }
        }

        /// <summary>
        /// Checks to see if message equals any movement questions.  If so, 
        /// the program calls the desired event
        /// </summary>
        /// <param name="message">string to compare to questions</param>
        public bool findQuestionAndLoadEvent(string message)
        {            
            bool movementExecuted = false;

            foreach (questionsAndEvents fireEvent in movementQuestions)
            {
                if (fireEvent.question == message)
                {
                    findEventInXmlFile(fireEvent.eventID);
                    movementExecuted = true;
                }
            }
            return movementExecuted;
        }              

        /// <summary>
        /// This method locates the event in the events file that is being called 
        /// and then calls loadObject() to process the event. If the event is not
        /// found, the file is closed.
        /// </summary>
        /// <param name="eventNum">Number that represents which event to processs</param>
        public void findEventInXmlFile(int eventNum) 
        {
            XmlTextReader reader = new XmlTextReader(eventFilePath);
           
            bool eventFound = false;

            try
            {
                while (reader.Read() && !eventFound && !eventExited)
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (reader.Name == "event" && !reader.IsEmptyElement)
                            {
                                while (reader.MoveToNextAttribute())
                                {
                                    try
                                    {
                                        string attribName = reader.Name;
                                        string attribValue = reader.Value;

                                        if (attribValue.Equals(eventNum.ToString())) 
                                        {
                                            eventFound = true;
                                            loadObject(reader);
                                        }
                                        
                                    }
                                    catch (FormatException fe)
                                    {
                                        System.Windows.Forms.MessageBox.Show("Error reading events file: \n\n" + fe.ToString());
                                    }
                                }
                            }
                            break;

                        case XmlNodeType.EndElement:
                            // Event not found.  Close the file.
                            if (reader.Name == "events")
                                eventFound = true;                            
                            break;
                    }
                }
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        /// This method processes which objects to invoke (BotMove, BotAcion, BotChat) 
        /// and calls the correct helper method.  When done processing the objects and
        /// the end of the event is reached, the file is closed.
        /// </summary>
        /// <param name="reader">Conatins the position in the XML file that needs processed</param>
        private void loadObject(XmlTextReader reader)
        {
            bool eventLoaded = false;

            while (reader.Read() && !eventLoaded && !eventExited)
            {
                //if (eventPaused)
                //    Thread.Sleep(1000);
                //else
                //{
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (reader.Name == "movement")
                                loadBotMoveMethod(reader);
                            else if (reader.Name == "action")
                                loadBotActionMethod(reader);
                            else if (reader.Name == "chat")
                                chat.loadChat(reader);
                            break;

                        case XmlNodeType.EndElement:
                            // End of event. Close the file.
                            if (reader.Name == "event")
                                eventLoaded = true;
                            break;
                    }
                //}
            }
        }

        /// <summary>
        /// This method is processed when a BotMove object is needed.  It determines 
        /// which method of BotMove needs to be called and then reads in the appropriate
        /// variables and calls the method.
        /// </summary>
        /// <param name="reader">Contains the position in the XML file that needs processed</param>
        private void loadBotMoveMethod(XmlTextReader reader)
        {              
            bool endOfMethod = false;

            while (reader.Read() && !endOfMethod && !eventExited)
            {
                switch (reader.NodeType)
                {
                    // Here we call the correct BotMove method.  Any method added to BotMove will
                    // need to be included here for processing.

                    case XmlNodeType.Element:
                        if (reader.Name == "moveTo")
                        {
                            // BotMove.moveTo() has been called and now we load the Vector needed
                            // for the method call                                             
                            botMovement.moveTo(readVectorCoord(reader));
                        }
                        else if (reader.Name == "Teleport")
                        {
                            bool methodLoaded = false;

                            while (reader.Read() && !methodLoaded && !eventExited)
                            {
                                switch (reader.NodeType)
                                {
                                    case XmlNodeType.Element:
                                        if (reader.Name == "Region")
                                        {
                                            reader.Read();
                                            botMovement.setRegionName(reader.Value);                                           
                                        }
                                        if (reader.Name == "Vector")
                                        {
                                            botMovement.teleportTo(readVectorCoord(reader));                                            
                                        }
                                        break;
                                    case XmlNodeType.EndElement:
                                        if (reader.Name == "Teleport")
                                            methodLoaded = true;
                                        break;
                                }
                            }
                        }
                        break;
                    case XmlNodeType.EndElement:
                        // Here we look for the closing element for the method name.  Once reached 
                        // we end the loop and give control back to calling method (loadObject)
                        if (reader.Name == "movement")
                            endOfMethod = true;
                        break;
                }
            }         
        }

        /// <summary>
        /// All Vector3 objects have and x, y, and z coordinate.  Here we will
        /// read 3 nodes in Xml that will contian the coordinates and then create 
        /// a Vector3 object and return it.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private Vector3 readVectorCoord(XmlTextReader reader)
        {             
            float[] variables = new float[3];            
            for (int i = 0; i < 3; i++)
            {
                reader.Read(); // read the whitespace
                reader.Read(); // read next element name                
                reader.Read(); // read next text coordinate value
                variables[i] = Single.Parse(reader.Value); // copy coordinate value into array
                reader.Read();  // read the end element                
            }
            return new Vector3(variables[0], variables[1], variables[2]);            
        }

        /// <summary>
        /// This method is processed when a BotAction object is needed. This will read what 
        /// variables need to be set for the action that needs to happen and then reads the 
        /// variables needed to call the createAction method (actionType and Time)
        /// </summary>
        /// <param name="reader">Contains the position in the XML file that needs processed</param>
        private void loadBotActionMethod(XmlTextReader reader)
        {            
            // Loads all variables needed and then calls BotAction.creatAction() 
            
            bool endOfMethod = false;
            int actionType = 0; // this will be set to the correct action number
            int time = 0;  // if needed, this will be set to alloted time for action

            while (reader.Read() && !endOfMethod && !eventExited)
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Name == "createAction")
                        {
                            bool methodLoaded = false;

                            // read and set the variables for this action
                            // and read the parameters for the createAction 
                            // method (actionType and time)
                            while (reader.Read() && !methodLoaded && !eventExited)
                            {
                                switch (reader.NodeType)
                                {
                                    case XmlNodeType.Element:
                                        if (reader.Name == "UUID")
                                        {
                                            reader.Read();
                                            action.setUUID(new UUID(reader.Value));
                                        }
                                        if (reader.Name == "localID")
                                        {
                                            reader.Read();
                                            uint tmpLocalID = uint.Parse(reader.Value);
                                            action.setLocalID(tmpLocalID);
                                        }
                                        else if (reader.Name == "InvItem")
                                        {
                                            reader.Read();
                                            UUID tmpUUID = new UUID(reader.Value);
                                            action.setInvItem((new InventoryItem(tmpUUID)));
                                        }
                                        else if (reader.Name == "AttachPoint")
                                        {
                                            reader.Read();
                                            AttachmentPoint point = (AttachmentPoint) Enum.Parse(typeof(AttachmentPoint), reader.Value,true);
                                            action.setAtchPoint(point);
                                        }
                                        else if (reader.Name == "Vector")
                                        {
                                            action.setVector3(readVectorCoord(reader));
                                        }
                                        else if (reader.Name == "actionType")
                                        {
                                            reader.Read();
                                            actionType = Int32.Parse(reader.Value);
                                        }
                                        else if (reader.Name == "Timer")
                                        {
                                            reader.Read();
                                            time = Int32.Parse(reader.Value);
                                        }
                                        else if (reader.Name == "SleepTime")
                                        {
                                            reader.Read();
                                            string randomTime = reader.Value;
                                            if (randomTime == "random")
                                                action.setRandom(true);
                                            else 
                                                action.setTime(Int32.Parse(randomTime));
                                        }
                                        break;
                                    case XmlNodeType.EndElement:
                                        // all variables should've loaded for action.                                        
                                        if (reader.Name == "createAction")
                                        {
                                            // call createAction to perform the action
                                            action.createAction(actionType, time);
                                            methodLoaded = true;
                                        }                                        
                                        break;
                                }
                            }

                        }
                        break;
                    case XmlNodeType.EndElement:
                        if (reader.Name == "action")
                            endOfMethod = true;
                        break;
                }
            }
        }
        /// <summary>
        /// Allows botload to directly issue a movement command
        /// </summary>
        /// <param name="destination">Vector3 that is the destination of the bot</param>
        public void moveBot(Vector3 destination){
            botMovement.moveTo(destination);
        }
        public void followAvatar(Avatar agent)
        {
            botMovement.chase(agent);
        }

        #endregion
    }     
}
