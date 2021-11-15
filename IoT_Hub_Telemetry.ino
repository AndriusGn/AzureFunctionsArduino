#include "arduino_secrets.h"

//to generate certs for auth
#include <ArduinoBearSSL.h>
#include <ArduinoECCX08.h>
#include <utility/ECCX08SelfSignedCert.h>

//mqtt client to connect to the cloud
#include <ArduinoMqttClient.h>
#include <WiFiNINA.h> // change to #include <WiFi101.h> for MKR1000

//sensor library (in our case we are interested into LM35 Temperature sensor)
//#include <Arduino_MKRENV.h>
#include <LM35.h>

//to handle Json data (serialization and desirialization)
#include <ArduinoJson.h>

/////// Enter your sensitive data in arduino_secrets.h
const char ssid[]        = SECRET_SSID;
const char pass[]        = SECRET_PASS;
const char broker[]      = SECRET_BROKER;
String     deviceId      = SECRET_DEVICEID;

int status = WL_IDLE_STATUS;

WiFiClient    wifiClient;            // Used for the TCP socket connection
BearSSLClient sslClient(wifiClient); // Used for SSL/TLS connection, integrates with ECC508
MqttClient    mqttClient(sslClient);

// Here is the code for using library LM35.h (previously it was code for Arduino_MKRENV.h)
/*
void setup() 
{
  Serial.begin(9600);

  if (!ENV.begin()) 
  {
    Serial.println("Failed to initialize MKR ENV shield!");
    while (1);
  }

  if (!ECCX08.begin()) 
  {
    Serial.println("No ECCX08 present!");
    while (1);
  }
 */
// Configuration: LM35 variable(pin);
LM35 temp(A0);

void setup()
{
  //Set the pin mode
  pinMode(A0, INPUT);

  //Set and start the serial port
  Serial.begin(9600);

  // reconstruct the self signed cert
  ECCX08SelfSignedCert.beginReconstruction(0, 8);
  ECCX08SelfSignedCert.setCommonName(ECCX08.serialNumber());
  ECCX08SelfSignedCert.endReconstruction();

  // Set a callback to get the current time
  // used to validate the servers certificate
  ArduinoBearSSL.onGetTime(getTime);

  // Set the ECCX08 slot to use for the private key
  // and the accompanying public certificate for it
  sslClient.setEccSlot(0, ECCX08SelfSignedCert.bytes(), ECCX08SelfSignedCert.length());

  // Set the client id used for MQTT as the device id
  mqttClient.setId(deviceId);

  mqttClient.setUsernamePassword(SECRET_USERNAME, SECRET_PASSWORD);

  // Set the message callback, this function is
  // called when the MQTTClient receives a message
  mqttClient.onMessage(onMessageReceived);

  while (status != WL_CONNECTED) 
  {
    Serial.print("Attempting to connect to SSID: ");
    Serial.println(ssid);
    // Connect to WPA/WPA2 network. Change this line if using open or WEP network:
    status = WiFi.begin(ssid, pass);

    delay(5000);
  }
}

void loop() {

  if (!mqttClient.connected()) {
    // MQTT client is disconnected, connect
    connectMQTT();
  }

  // poll for new MQTT messages and send keep alives
  mqttClient.poll();

  publishMessage();
  

  // wait one minute
  delay(60000);
}

unsigned long getTime() {
  // get the current time from the WiFi module
  return WiFi.getTime();
}



void connectMQTT() {
  Serial.print("Attempting to MQTT broker: ");
  Serial.print(broker);
  Serial.println(" ");

  while (!mqttClient.connect(broker, 8883)) {
    // failed, retry
    switch (abs(mqttClient.connectError()))
    {
    case 1:
      Serial.println("Unacceptable protocol version");
      break;
    case 2:
      Serial.println("Identifier rejected");
      break;
    case 3:
      Serial.println("Server Unavailable");
      break;
    case 4:
      Serial.println("Bad username or password");
      break;
    case 5:
      Serial.println("Not authorized");
      break;
    default:
      Serial.println("Unknown error code");
      break;
    }
    
    //Serial.print(".");
    //Serial.println(mqttClient.connectError());
    delay(5000);
  }
  Serial.println();

  Serial.println("You're connected to the MQTT broker");
  Serial.println();

  // subscribe to a topic
  mqttClient.subscribe("devices/" + deviceId + "/messages/devicebound/#");
}

void publishMessage() {
  StaticJsonDocument<300> doc;
  Serial.println("Publishing message");
  /*
  float temperature = ENV.readTemperature(FAHRENHEIT);
  float humidity    = ENV.readHumidity();
  float pressure    = ENV.readPressure(PSI);
  float illuminance = ENV.readIlluminance(FOOTCANDLE);
  */

  float temperature_cel = temp.cel();
  float temperature_fah = temp.fah();
  float temperature_k   = temp.kel();
  String id             = deviceId + getTime(); // Needed for CosmosDB

  //This part is not needed for Json constructor:
  /*
  Serial.print("Temp - ");  
  Serial.print(temp.cel()); // Converts the data to Celsius and sends to serial
  Serial.print(" C  - ");
  Serial.print(temp.fah()); // Converts the data to Fahrenheit and sends to serial
  Serial.print(" F  - ");   
  Serial.print(temp.kel()); // Converts the data to Kelvin and sends to serial
  Serial.println(" K");
  //Serial.println(" "); // Linha em Branco
  */
  
  // send message, the Print interface can be used to set the message contents
  mqttClient.beginMessage("devices/" + deviceId + "/messages/events/");
  doc["id"] = id;
  doc["deviceId"] = deviceId;
  doc["timestamp"] = getTime();
  doc["temp_cel"] = temperature_cel;
  doc["temp_fah"] = temperature_fah;
  doc["temp_k"] = temperature_k;
  /*
  doc["temp"] = temperature;
  doc["humidity"] = humidity;
  doc["pressure"] = pressure;
  doc["illuminance"] = illuminance;
  */
  serializeJson(doc,Serial);
  serializeJson(doc, mqttClient);
  mqttClient.endMessage();

  Serial.println();
}

void onMessageReceived(int messageSize) {
  // we received a message, print out the topic and contents
  Serial.print("Received a message with topic '");
  Serial.print(mqttClient.messageTopic());
  Serial.print("', length ");
  Serial.print(messageSize);
  Serial.println(" bytes:");

  // use the Stream interface to print the contents
  while (mqttClient.available()) {
    Serial.print((char)mqttClient.read());
  }
  Serial.println();

  Serial.println();
}
