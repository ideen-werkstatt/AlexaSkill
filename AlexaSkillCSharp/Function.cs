using System;
using System.Collections.Generic;
using System.Linq;
using Amazon;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization;
using Alexa.NET.Response;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Newtonsoft.Json;
using System.Collections.Generic;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using System.Threading.Tasks;
using Alexa.NET;
using System.IO;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AlexaSkillCSharp
{
    public class Function
    {

        // Replace sender@example.com with your "From" address.
        // This address must be verified with Amazon SES.
        static readonly string senderAddress = "stefanroehrig@gmx.de";

        // Replace recipient@example.com with a "To" address. If your account
        // is still in the sandbox, this address must be verified.
        static readonly string receiverAddress = "kdmytrok@gmail.com";

        // The configuration set to use for this email. If you do not want to use a
        // configuration set, comment out the following property and the
        // ConfigurationSetName = configSet argument below. 
        static readonly string configSet = "ConfigSet";

        // The subject line for the email.
        static readonly string subject = "Alexa informiert";

        // The email body for recipients with non-HTML email clients.
        static readonly string textBody = "";

        //// The HTML body of the email.<h1>Amazon SES Test (AWS SDK for .NET)</h1>
        //static string htmlBody = @"<html>
        //                                <head></head>
        //                                <body>
        //                                  <p>Diese Email informiert Sie über
        //                                   #Inhalt#
        //                                   <br>Weitergehende Informationen bekommen Sie auf der Website des Konzerns:</br>
        //                                  <br><a href='http://www.hallesche.de/'> Hallesche</a> </br>
        //                                  <br><a href='http://www.alte-leipziger.de/'> Alte Leipziger</a> </br>  
        //                                 </p>
        //                                </ body >
        //                                </ html > ";

        enum STATES
        {
            Default,
            GetAllgemeineInfosKonzern,
            GetVersicherungsprodukte,
            GetVersicherungsprodukt,
            GetStammdaten,
            GetKundenNummer,
            GetStammdatenArt,
            GetStammdatenAendern,
            Help,
            UnhandledMessage
        };

        enum HILFSTATES
        {
            GetGesellschaft,
            HandleGetVersicherungsprodukteIntent
        }

        public List<string> lstValuesGesellschaft = new List<string>()
        {
            "alte leipziger",
            "alten leipziger",
            "alte leipziger lebensversicherung",
            "alten leipziger lebensversicherung",
            "hallesche",
            "halleschen",
            "hallesche krankenversicherung",
            "halleschen krankenversicherung",
            "konzern",
            "konzerns",
            "gesamten konzern",
            "gesamten konzerns"
        };

        public List<string> lstValuesVersicherungsprodukte = new List<string>()
        {
            "private Krankenversicherungen",
            "pflegezusatzversicherungen",
            "pflegezusatzversicherung",
            "zahnzusatzversicherungen",
            "zahnzusatzversicherung",
            "krankenhaus zusatzversicherungen",
            "krankentagegeld",
            "auslandsreise und Krankenversicherungen",
            "gruppenversicherungen für Firmen und Verbände",
            "gruppenversicherungen",
            "fondsrenten",
            "moderne flexible renten",
            "moderne klassische rente",
            "berufsunfähigkeitsversicherung",
            "risikolebensversicherung",
            "betriebliche altersversorgung",
            "haftpflichtversicherung",
            "hausratversicherung",
            "rechtschutzversicherung",
            "unfallversicherung",
            "wohngebäudeversicherung",
            "reise und freizeitversicherung"
        };

        ILambdaLogger log;
        MessageRessource messageRessource;

        //SessionId um die zeitliche Abfolge der User-Request verfolgen zu koennen
        int sessionId = 0;

        Dictionary<string, object> attributes = new Dictionary<string, object>();
        Dictionary<string, object> hilfsAttributes = new Dictionary<string, object>();

        string gesellschaft = "";
        string versicherungsprodukt = "";
        string telefonnummer = "";
        string kundennummer = "";
        //string stammdatenart = "";



        public void GetMessageRessources()
        {
            //Standard-Phrasen
            messageRessource = new MessageRessource();
            messageRessource.WelcomeMessage = "Willkommen im neuen Alexa Skill des Alte Leipziger Hallesche Konzerns";
            messageRessource.SkillName = "Alte Leipziger Hallesche Skill";
            messageRessource.HelpMessage = "Ich kann dir Informationen über den Konzern geben, über Versicherungsprodukte informieren oder möchtest du Änderungen an deinen Stammdaten vornehmen?";
            messageRessource.StopMessage = "Auf Wiedersehen, bis zum nächsten Mal!";
            messageRessource.UnhandledMessage = "Ich habe dich leider nicht verstanden";

            //Phrasen
            messageRessource.AllgemeineInformationenAlteLeipziger = $"Allgemeine Informationen zur Alten Leipziger Lebensversicherung. {messageRessource.StopMessage}";
            messageRessource.AllgemeineInformationenHallesche = $"Allgemeine Informationen zur Halleschen Krankenversicherung. {messageRessource.StopMessage}";
            messageRessource.AllgemeineInformationenKonzern = $"Allgemeine Informationen zum Alten Leipziger Halleschen Konzern. {messageRessource.StopMessage}";
            messageRessource.VersicherungsprodukteAlteLeipziger = "Die Alte Leipziger Lebensversicherung bietet beispielsweise Fondsrenten und Berufsunfähigkeitsversicherungen an. Möchtest du nähere Informationen zu einem genannten Versicherungsprodukt erhalten? Falls ja, nenne mir einfach den Namen!";
            messageRessource.VersicherungsprodukteHallesche = "Die Hallesche Krankenversicherung bietet beispielsweise zahnzusatzversicherungen und Pflegezusatzversicherungen an. Möchtest du nähere Informationen zu einem genannten Versicherungsprodukt erhalten? Falls ja, nenne mir einfach den Namen!?";
            messageRessource.VersicherungsprodukteKonzern = "Der Alte Leipziger Hallesche Konzern bietet beispielsweise Fondsrenten, Berufsunfähigkeitsversicherungen, private Krankenversicherung und Pflegezusatzversicherungen an. Möchtest du nähere Informationen zu einem genannten Versicherungsprodukt erhalten? Falls ja, nenne mir einfach den Namen!?";
        }

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public SkillResponse FunctionHandler(SkillRequest input, ILambdaContext context)
        {

            SkillResponse response = new SkillResponse();
            response.Response = new ResponseBody();
            response.Response.ShouldEndSession = false;
            IOutputSpeech innerResponse = null;
            log = context.Logger;
            log.LogLine($"Skill Request Object:");
            log.LogLine(JsonConvert.SerializeObject(input));

            sessionId += 1;
            // Convert.ToInt32(input.Session.SessionId) + 1;
            //var attributes = input.Session.Attributes;

            this.GetMessageRessources();

            if (input.GetRequestType() == typeof(LaunchRequest))
            {
                log.LogLine($"Default LaunchRequest made: 'Alexa, öffne AL");
                response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                {
                    Text = messageRessource.WelcomeMessage + messageRessource.HelpMessage
                }, new Reprompt()
                {
                    OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                });
                response.Response.ShouldEndSession = false;
            }
            else if (input.GetRequestType() == typeof(IntentRequest))
            {
                var intentRequest = (IntentRequest)input.Request;

                //Um zwischen Einwort-Antworten zu unterscheiden.
                if (!hilfsAttributes.LastOrDefault().Equals(new KeyValuePair<string, object>()))
                {
                    log.LogLine($"Steht was drin");
                    if ((HILFSTATES)hilfsAttributes?.LastOrDefault().Value == HILFSTATES.GetGesellschaft)
                    {
                        intentRequest.Intent.Name = "GetGesellschaft";
                    }
                    else if ((HILFSTATES)hilfsAttributes?.LastOrDefault().Value == HILFSTATES.HandleGetVersicherungsprodukteIntent)
                    {
                        intentRequest.Intent.Name = "GetVersicherungsprodukt";
                    }

                    hilfsAttributes.Clear();
                }


                switch (intentRequest.Intent.Name)
                {
                    case "AMAZON.CancelIntent":
                        log.LogLine($"AMAZON.CancelIntent: send StopMessage");
                        response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
                        {
                            Text = messageRessource.StopMessage
                        });
                        response.Response.ShouldEndSession = true;
                        break;
                    case "AMAZON.StopIntent":
                        log.LogLine($"AMAZON.StopIntent: send StopMessage");
                        response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
                        {
                            Text = messageRessource.StopMessage
                        });
                        response.Response.ShouldEndSession = true;
                        break;
                    case "AMAZON.HelpIntent":
                        log.LogLine($"AMAZON.HelpIntent: send HelpMessage");
                        //Status in Dictionary aufnehmen
                        attributes.Add(sessionId.ToString(), STATES.Help);
                        response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
                        {
                            Text = "HelpHandler im Handler " + messageRessource.HelpMessage
                        });
                        break;

                    case "GetAllgemeineInfosKonzern":
                        log.LogLine($"GetAllgemeineInfosKonzern sent");
                        //Status in Dictionary aufnehmen
                        attributes.Add(sessionId.ToString(), STATES.GetAllgemeineInfosKonzern);
                        innerResponse = new PlainTextOutputSpeech();

                        gesellschaft = !string.IsNullOrEmpty(intentRequest.Intent.Slots["Gesellschaft"]?.Value) ? intentRequest.Intent.Slots["Gesellschaft"]?.Value : string.Empty;

                        response = this.HandleGetAllgemeineInfosKonzernIntent(gesellschaft, response);
                        break;

                    case "GetVersicherungsprodukte":
                        log.LogLine($"GetVersicherungsprodukteIntent sent");
                        //Status in Dictionary aufnehmen
                        attributes.Add(sessionId.ToString(), STATES.GetVersicherungsprodukte);
                        innerResponse = new PlainTextOutputSpeech();

                        gesellschaft = !string.IsNullOrEmpty(intentRequest.Intent.Slots["Gesellschaft"]?.Value) ? intentRequest.Intent.Slots["Gesellschaft"]?.Value : string.Empty;

                        log.LogLine($"GetVersicherungsprodukteIntent Gesellschaft set to " + gesellschaft);

                        response = this.HandleGetVersicherungsprodukteIntent(gesellschaft, response);
                        break;

                    case "GetStammdaten":
                        log.LogLine($"GetStammdaten sent");
                        attributes.Add(sessionId.ToString(), STATES.GetStammdaten);

                        response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                        {
                            Text = $"Ok, du möchtest also deine Stammdaten ändern. Teile mir deine Kundennummer in folgender Weise mit . meine kundennummer ist  ."
                        }, new Reprompt()
                        {
                            OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                        });
                        break;

                    case "GetKundenNummer":
                        {
                            kundennummer = "";
                            log.LogLine($"GetKundenNummer sent");
                            var lastIntent = attributes.LastOrDefault();

                            if (!lastIntent.Equals(new KeyValuePair<string, object>()))
                            {
                                if ((STATES)lastIntent.Value == STATES.GetStammdaten)
                                {
                                    attributes.Add(sessionId.ToString(), STATES.GetKundenNummer);
                                    kundennummer = intentRequest.Intent.Slots["Kundennummer"].Value;
                                    log.LogLine($"Kundennummer set to " + kundennummer);
                                    int knrCheck;
                                    if (int.TryParse(kundennummer, out knrCheck))
                                    {
                                        response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                        {
                                            Text = $"Möchtest du deine Telefonnummer oder Bankverbindung ändern?  ."
                                        }, new Reprompt()
                                        {
                                            OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                        });
                                    }
                                    else
                                    {
                                        log.LogLine($"GetKundenNummer sent innerestes else");
                                        response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                        {
                                            Text = messageRessource.HelpMessage
                                        }, new Reprompt()
                                        {
                                            OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                        });
                                        response.Response.ShouldEndSession = false;
                                        this.attributes.Clear();
                                    }


                                }
                                else
                                {
                                    log.LogLine($"GetKundenNummer sent inneres else");
                                    response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                    {
                                        Text = messageRessource.HelpMessage
                                    }, new Reprompt()
                                    {
                                        OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                    });
                                    response.Response.ShouldEndSession = false;
                                    this.attributes.Clear();
                                }
                            }
                            else
                            {
                                log.LogLine($"GetKundenNummer sent äußeres else");
                                response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                {
                                    Text = messageRessource.HelpMessage
                                }, new Reprompt()
                                {
                                    OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                });
                                this.attributes.Clear();
                                response.Response.ShouldEndSession = false;

                            }
                        }
                        break;

                    case "GetStammdatenArt":
                        {
                            log.LogLine($"GetStammdatenArt sent");
                            var lastIntent = attributes.LastOrDefault();

                            if (!lastIntent.Equals(new KeyValuePair<string, object>()))
                            {
                                if ((STATES)lastIntent.Value == STATES.GetKundenNummer)
                                {
                                    attributes.Add(sessionId.ToString(), STATES.GetStammdatenArt);
                                    response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                    {
                                        Text = $"Ok, du willst also deine Telefonnummer ändern. Dann sag bitte . meine neue telefonnummer ist"
                                    }, new Reprompt()
                                    {
                                        OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                    });
                                }
                                else
                                {
                                    response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                    {
                                        Text = messageRessource.HelpMessage
                                    }, new Reprompt()
                                    {
                                        OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                    });
                                    response.Response.ShouldEndSession = false;
                                    this.attributes.Clear();
                                }
                            }
                            else
                            {
                                response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                {
                                    Text = messageRessource.HelpMessage
                                }, new Reprompt()
                                {
                                    OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                });
                                response.Response.ShouldEndSession = false;
                                this.attributes.Clear();

                            }
                            break;

                        }

                    case "GetStammdatenAendern":
                        {
                            telefonnummer = "";
                            log.LogLine($"GetStammdatenAendern sent");
                            var lastIntent = attributes.LastOrDefault();

                            if (!lastIntent.Equals(new KeyValuePair<string, object>()))
                            {
                                if ((STATES)lastIntent.Value == STATES.GetStammdatenArt)
                                {
                                    attributes.Add(sessionId.ToString(), STATES.GetStammdatenAendern);
                                    telefonnummer = intentRequest.Intent.Slots["Aenderung"].Value;
                                    log.LogLine($"Telefonnummer set to " + telefonnummer);

                                    int tnrCheck;
                                    if (int.TryParse(telefonnummer, out tnrCheck))
                                    {
                                        response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                        {
                                            Text = $"Ok, soll ich deine Telefonnumer zu der Nummer {telefonnummer} wirklich ändern?"
                                        }, new Reprompt()
                                        {
                                            OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                        });
                                    }
                                    else
                                    {
                                        response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                        {
                                            Text = messageRessource.HelpMessage
                                        }, new Reprompt()
                                        {
                                            OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                        });
                                        response.Response.ShouldEndSession = false;
                                        this.attributes.Clear();
                                    }
                                }
                            }
                            else
                            {
                                response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                {
                                    Text = messageRessource.HelpMessage
                                }, new Reprompt()
                                {
                                    OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                });
                                response.Response.ShouldEndSession = false;
                                this.attributes.Clear();

                            }

                            break;
                        }

                    case "GetGesellschaft":
                        {
                            log.LogLine($"GetGesellschaft sent");
                            gesellschaft = "";
                            var lastIntent = attributes.LastOrDefault();
                            log.LogLine($"GetGesellschaft sent " + lastIntent);
                            if (!lastIntent.Equals(new KeyValuePair<string, object>()))
                            {
                                if ((!intentRequest.Intent.Slots.ContainsKey("Gesellschaft"))
                                    || (intentRequest.Intent.Slots["Gesellschaft"].Value == null)
                                    || (intentRequest.Intent.Slots["Gesellschaft"].Equals(new KeyValuePair<string, object>()))
                                    || (!this.checkGesellschaft(intentRequest.Intent.Slots["Gesellschaft"].Value.ToLower())))
                                {
                                    log.LogLine($"Drin");
                                    response = this.GetGesellschaft();
                                }
                                else
                                {

                                    //Geht das auch mit Schleife und allen möglichen inhalten des Slot-Types? ausprobieren!
                                    switch ((STATES)lastIntent.Value)
                                    {
                                        //Prüfen, ob Gesellschaft vorhanden, bevor es befüllt wird, ansonsten nochmal nach Gesellschaft fragen.
                                        case STATES.GetAllgemeineInfosKonzern:

                                            gesellschaft = intentRequest.Intent.Slots["Gesellschaft"].Value;
                                            log.LogLine($"GetAllgemeineInfosKonzern set to " + intentRequest.Intent.Slots["Gesellschaft"].Value);

                                            response = this.HandleGetAllgemeineInfosKonzernIntent(gesellschaft, response);
                                            hilfsAttributes.Clear();
                                            break;
                                        case STATES.GetVersicherungsprodukte:
                                            gesellschaft = intentRequest.Intent.Slots["Gesellschaft"].Value;
                                            log.LogLine($"GetAllgemeineInfosKonzern set to " + intentRequest.Intent.Slots["Gesellschaft"].Value);

                                            response = this.HandleGetVersicherungsprodukteIntent(gesellschaft, response);
                                            hilfsAttributes.Clear();
                                            break;
                                        default:
                                            // Wenn nicht, dann nochmal nach Gesellschaft fragen
                                            //TODO: Wenn keiner der vorherigen Intents zutrifft, muss Alexa das preisgeben. UnhandledMessage oder nochmal etwas neues?
                                            log.LogLine($"GetGesellschaft sent default " + lastIntent);
                                            break;
                                    }

                                }
                            }
                            //TODO: Kein vorhergehender Intent vorhanden.
                            else
                            {
                                response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                {
                                    Text = messageRessource.HelpMessage + " GetGesellschaft"
                                }, new Reprompt()
                                {
                                    OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                });

                                this.attributes.Clear();
                                this.hilfsAttributes.Clear();
                                response.Response.ShouldEndSession = false;

                            }
                        }

                        break;

                    case "GetVersicherungsprodukt":
                        {
                            log.LogLine($"GetVersicherungsprodukt sent");

                            hilfsAttributes.Add(sessionId.ToString(), HILFSTATES.HandleGetVersicherungsprodukteIntent);

                            //versicherungsprodukt = "Keine";
                            var lastIntent = attributes.LastOrDefault();
                            var lastHelpIntent = hilfsAttributes.LastOrDefault();

                            attributes.Add(sessionId.ToString(), STATES.GetVersicherungsprodukt);

                            //log.LogLine($"GetGesellschaft sent " + lastIntent);
                            if (!lastIntent.Equals(new KeyValuePair<string, object>()))
                            {
                                log.LogLine($"GetVersicherungsprodukt sent " + lastIntent + ", " + lastHelpIntent);
                                //Geht das auch mit Schleife und allen möglichen inhalten des Slot-Types? ausprobieren!
                                if ((STATES)lastIntent.Value == STATES.GetVersicherungsprodukte || (HILFSTATES)lastHelpIntent.Value == HILFSTATES.HandleGetVersicherungsprodukteIntent)
                                {
                                    log.LogLine($"GetVersicherungsprodukt sent");
                                    //Prüfen ob es Versicherungsprodukt gibt, bevor es gefüllt wird, ansonsten nochmal nach Versicherungsprodukt fragen.

                                    //! intentRequest.Intent.Slots.ContainsKey("Versicherungsprodukt") 
                                    if ((!intentRequest.Intent.Slots.ContainsKey("Versicherungsprodukt"))
                                        || (intentRequest.Intent.Slots["Versicherungsprodukt"].Value == null)
                                        || (intentRequest.Intent.Slots["Versicherungsprodukt"].Equals(new KeyValuePair<string, object>()))
                                        || (!this.checkVersicherungsprodukt(intentRequest.Intent.Slots["Versicherungsprodukt"].Value.ToLower())))
                                    {
                                        log.LogLine($"GetVersicherungsprodukt sent");
                                        response = this.HandleGetVersicherungsprodukteIntent(gesellschaft, response);
                                    }
                                    else
                                    {
                                        versicherungsprodukt = intentRequest.Intent.Slots["Versicherungsprodukt"].Value;
                                        log.LogLine($"GetVersicherungsprodukt set to " + intentRequest.Intent.Slots["Versicherungsprodukt"].Value);

                                        //weitergehende Infos besorgen und eine Mail damit senden. Davor fragen, ob eine Mail gesendet werden darf?
                                        response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                        {
                                            Text = $"Darf ich dir eine Email mit weitergehenden Informationen zu {versicherungsprodukt} senden?"
                                        }, new Reprompt()
                                        {
                                            OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                        });

                                        hilfsAttributes.Clear();
                                    }

                                    response.Response.ShouldEndSession = false;

                                }
                                else
                                {
                                    // Wenn nicht, dann nochmal nach Versicherungsprodukt fragen
                                    //TODO: Wenn keiner der vorherigen Intents zutrifft, muss Alexa das preisgeben. UnhandledMessage oder nochmal etwas neues?
                                    response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                    {
                                        Text = messageRessource.HelpMessage + " GetVersicherungsprodukt Status nicht richtig"
                                    }, new Reprompt()
                                    {
                                        OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                    });
                                    response.Response.ShouldEndSession = false;
                                }

                            }
                            //TODO: Kein vorhergehender Intent vorhanden.
                            else
                            {
                                response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                {
                                    Text = messageRessource.HelpMessage + " GetVersicherungsprodukt lastIntent leer"
                                }, new Reprompt()
                                {
                                    OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                });
                                this.attributes.Clear();
                                this.hilfsAttributes.Clear();
                                response.Response.ShouldEndSession = false;

                            }
                        }

                        break;

                    case "AMAZON.YesIntent":
                        {
                            log.LogLine($"AMAZON.YesIntent sent zu " + versicherungsprodukt);
                            var lastIntent = attributes.LastOrDefault();
                            //log.LogLine($"GetGesellschaft sent " + lastIntent);
                            if (!lastIntent.Equals(new KeyValuePair<string, object>()))
                            {
                                //Geht das auch mit Schleife und allen möglichen inhalten des Slot-Types? ausprobieren!
                                if ((STATES)lastIntent.Value == STATES.GetVersicherungsprodukt)
                                {
                                    this.sendingMail(" das Versicherungsprodukt: " + versicherungsprodukt);
                                    response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
                                    {
                                        Text = $"Ok, ich habe dir eine Email mit weitergehenden Informationen über {versicherungsprodukt} gesendet.{messageRessource.StopMessage}"
                                    });
                                    //response.Response.ShouldEndSession = true;
                                }
                                else if ((STATES)lastIntent.Value == STATES.GetStammdatenAendern)
                                {
                                    this.sendingMail(" die Änderung Ihrer Telefonnummer: " + telefonnummer + ". Die Kundennummer ist :" + kundennummer);
                                    response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
                                    {
                                        Text = $"Ok, ich habe dir eine Bestätigung der Telefonnummeränderung per Email gesendet.{messageRessource.StopMessage}"
                                    });

                                }

                            }
                            //TODO: Kein vorhergehender bzw. passender Intent (GetVersicherungsprodukt) vorhanden.
                            else
                            {
                                response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                {
                                    Text = "YesIntent im Handler " + messageRessource.HelpMessage
                                }, new Reprompt()
                                {
                                    OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                });
                                response.Response.ShouldEndSession = false;
                            }
                        }

                        break;

                    case "AMAZON.NoIntent":
                        {
                            log.LogLine($"AMAZON.NoIntent sent");
                            var lastIntent = attributes.LastOrDefault();
                            //log.LogLine($"GetGesellschaft sent " + lastIntent);
                            if (!lastIntent.Equals(new KeyValuePair<string, object>()))
                            {
                                //Geht das auch mit Schleife und allen möglichen inhalten des Slot-Types? ausprobieren!
                                if ((STATES)lastIntent.Value == STATES.GetVersicherungsprodukt || (STATES)lastIntent.Value == STATES.GetStammdatenAendern)
                                {
                                    response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
                                    {
                                        Text = $"Ok, ich habe dir keine Email gesendet.{messageRessource.StopMessage}"
                                    });
                                    response.Response.ShouldEndSession = true;
                                }
                            }
                            //TODO: Kein vorhergehender bzw. passender Intent (GetVersicherungsprodukt) vorhanden.
                            else
                            {
                                response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                                {
                                    Text = "NoIntent im Handler " + messageRessource.HelpMessage
                                }, new Reprompt()
                                {
                                    OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                                });
                                response.Response.ShouldEndSession = false;

                            }

                        }

                        break;

                    //Erweiterte Hilfestellung bei Gesellschaft und Versicherungsprodukt
                    default:
                        {
                            log.LogLine($"Unknown intent: " + intentRequest.Intent.Name);
                            var lastIntent = hilfsAttributes.LastOrDefault();
                            //log.LogLine($"GetGesellschaft sent " + lastIntent);
                            if (!lastIntent.Equals(new KeyValuePair<string, object>()))
                            {
                                if ((HILFSTATES)lastIntent.Value == HILFSTATES.GetGesellschaft)
                                {
                                    this.GetGesellschaft();
                                }
                                else if ((HILFSTATES)lastIntent.Value == HILFSTATES.HandleGetVersicherungsprodukteIntent)
                                {
                                    this.HandleGetVersicherungsprodukteIntent(gesellschaft, response);
                                }
                            }
                            else
                            {
                                response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
                                {
                                    Text = messageRessource.UnhandledMessage
                                });
                            }

                            response.Response.ShouldEndSession = false;
                        }

                        break;
                }
            }
            //ResponseBuilder
            //response.Response.OutputSpeech = innerResponse;
            response.Version = "1.0";
            log.LogLine($"Skill Response Object...");
            log.LogLine(JsonConvert.SerializeObject(response));
            return response;
        }

        public bool checkVersicherungsprodukt(string versicherungsprodukt)
        {
            return this.lstValuesVersicherungsprodukte.Contains(versicherungsprodukt.ToLower());
        }

        public bool checkGesellschaft(string gesellschaft)
        {
            return this.lstValuesGesellschaft.Contains(gesellschaft.ToLower());
        }

        public SkillResponse HandleGetVersicherungsprodukteIntent(string gesellschaft, SkillResponse response)
        {
            hilfsAttributes.Clear();// = new Dictionary<string, object>();
            if (gesellschaft != "")
            {
                switch (gesellschaft)
                {
                    case "hallesche":
                    case "hallesche krankenversicherung":
                        log.LogLine($"GetVersicherungsprodukte: Gesellschaft {gesellschaft}");
                        response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                        {
                            Text = messageRessource.VersicherungsprodukteHallesche
                            //"Informationen über Versicherungsprodukte der Halleschen Krankenversicherung. Versicherungsprodukt?"
                        }, new Reprompt()
                        {
                            OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                        });
                        response.Response.ShouldEndSession = false;
                        hilfsAttributes.Add(sessionId.ToString(), HILFSTATES.HandleGetVersicherungsprodukteIntent);
                        break;
                    case "alte leipziger":
                    case "alte leipziger lebensversicherung":
                        log.LogLine($"GetVersicherungsprodukte: Gesellschaft {gesellschaft}");
                        response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                        {
                            Text = messageRessource.VersicherungsprodukteAlteLeipziger
                            //"Informationen über Versicherungsprodukte der Alten Leipziger Lebensversicherung. Versicherungsprodukt?"
                        }, new Reprompt()
                        {
                            OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                        });
                        response.Response.ShouldEndSession = false;
                        hilfsAttributes.Add(sessionId.ToString(), HILFSTATES.HandleGetVersicherungsprodukteIntent);
                        break;
                    case "konzern":
                    case "gesamten konzern":
                        log.LogLine($"GetVersicherungsprodukte: Gesellschaft {gesellschaft}");
                        response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
                        {
                            Text = messageRessource.VersicherungsprodukteKonzern
                            //"Informationen über Versicherungsprodukte des gesamten Konzerns. Versicherungsprodukt?"
                        }, new Reprompt()
                        {
                            OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
                        });
                        response.Response.ShouldEndSession = false;
                        hilfsAttributes.Add(sessionId.ToString(), HILFSTATES.HandleGetVersicherungsprodukteIntent);
                        break;
                }
            }
            // gesellschaft unbekannt
            else
            {
                response = this.GetGesellschaft();
                response.Response.ShouldEndSession = false;
            }
            return response;
        }


        public SkillResponse HandleGetAllgemeineInfosKonzernIntent(string gesellschaft, SkillResponse response)
        {
            if (gesellschaft != "")
            {
                switch (gesellschaft)
                {
                    case "hallesche":
                    case "hallesche krankenversicherung":
                        response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
                        {
                            Text = messageRessource.AllgemeineInformationenHallesche
                            //$"Informationen über die Hallesche Krankenversicherung.{messageRessource.StopMessage}"
                        });
                        log.LogLine($"GetAllgemeineInfosKonzern: Gesellschaft {gesellschaft}");
                        break;
                    case "alte leipziger":
                    case "alte leipziger lebensversicherung":
                        response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
                        {
                            Text = messageRessource.AllgemeineInformationenAlteLeipziger
                            //$"Informationen über die Alte Leipziger Lebensversicherung.{messageRessource.StopMessage}"
                        });
                        //(innerResponse as PlainTextOutputSpeech).Text = "Informationen über die Alte Leipziger Lebensversicherung";
                        log.LogLine($"GetAllgemeineInfosKonzern: Gesellschaft {gesellschaft}");
                        break;
                    case "konzern":
                    case "gesamten konzern":
                        response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
                        {
                            Text = messageRessource.AllgemeineInformationenKonzern
                            //$"Informationen über die Alte Leipziger Lebensversicherung.{messageRessource.StopMessage}"
                        });
                        log.LogLine($"GetAllgemeineInfosKonzern: Gesellschaft {gesellschaft}");
                        break;
                }

                response.Response.ShouldEndSession = true;
            }
            // gesellschaft unbekannt
            else
            {
                response = this.GetGesellschaft();
                response.Response.ShouldEndSession = false;
            }
            return response;
        }

        public SkillResponse GetGesellschaft()
        {
            hilfsAttributes.Clear();// = new Dictionary<string, object>();
            var response = ResponseBuilder.Ask(new PlainTextOutputSpeech()
            {
                Text = "Welche Gesellschaft meinst du? Hallesche Krankenversicherung, Alte Leipziger Lebensversicherung oder den gesamten Konzern"
            }, new Reprompt()
            {
                OutputSpeech = new PlainTextOutputSpeech { Text = "Reprompt" },
            });
            hilfsAttributes.Add(sessionId.ToString(), HILFSTATES.GetGesellschaft);
            return response;

        }

        public void sendingMail(string inhalt)
        {
            log.LogLine($"sendingMail Inhalt: {inhalt}");
            // The HTML body of the email.<h1>Amazon SES Test (AWS SDK for .NET)</h1>
            string htmlBody = @"<html>
                                        <head></head>
                                        <body>
                                          <p>Diese Email informiert Sie über
                                           #Inhalt#
                                           <br>Weitergehende Informationen bekommen Sie auf der Website des Konzerns:</br>
                                          <br><a href='http://www.hallesche.de/'> Hallesche</a> </br>
                                          <br><a href='http://www.alte-leipziger.de/'> Alte Leipziger</a> </br>  
                                         </p>
                                        </ body >
                                        </ html > ";

            if (telefonnummer != "" || versicherungsprodukt != "")
            {
                log.LogLine($"If-Fall Inhalt: {inhalt}");
                htmlBody = htmlBody.Replace("#Inhalt#", inhalt);
            }
            else
            {
                log.LogLine($"Else-Fall Inhalt: {inhalt}");
                htmlBody = htmlBody.Replace("#Inhalt#", "");
            }

            // Replace USWest2 with the AWS Region you're using for Amazon SES.
            // Acceptable values are EUWest1, USEast1, and USWest2.
            using (var client = new AmazonSimpleEmailServiceClient(RegionEndpoint.EUWest1))
            {
                var sendRequest = new SendEmailRequest
                {
                    Source = senderAddress,
                    Destination = new Destination
                    {
                        ToAddresses =
                        new List<string> { receiverAddress }
                    },
                    Message = new Message
                    {

                        Subject = new Content(subject),
                        Body = new Body
                        {
                            Html = new Content
                            {
                                Charset = "UTF-8",
                                Data = htmlBody
                            },
                            Text = new Content
                            {
                                Charset = "UTF-8",
                                Data = textBody
                            }
                        }
                    },
                    // If you are not using a configuration set, comment
                    // or remove the following line 
                    //ConfigurationSetName = configSet
                };

                try
                {
                    log.LogLine("Sending email using Amazon SES...");
                    var response = client.SendEmailAsync(sendRequest);
                    response.Wait();
                    log.LogLine("The email was sent successfully.");
                }
                catch (Exception ex)
                {
                    log.LogLine("The email was not sent.");
                    log.LogLine("Error message: " + ex.Message);

                }
            }

            //Console.Write("Press any key to continue...");
            //Console.ReadKey();
        }

    }

    public class MessageRessource
    {
        //Standard-Phrasen
        public string SkillName { get; set; }
        public string HelpMessage { get; set; }
        public string HelpReprompt { get; set; }
        public string StopMessage { get; set; }
        public string WelcomeMessage { get; set; }
        public string UnhandledMessage { get; set; }

        //Phrasen
        public string AllgemeineInformationenAlteLeipziger { get; set; }
        public string AllgemeineInformationenHallesche { get; set; }
        public string AllgemeineInformationenKonzern { get; set; }
        public string VersicherungsprodukteAlteLeipziger { get; set; }
        public string VersicherungsprodukteHallesche { get; set; }
        public string VersicherungsprodukteKonzern { get; set; }
    }
}


