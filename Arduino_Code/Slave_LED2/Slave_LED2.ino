#include <SPI.h>//This gives you an SPIClass, and an instance of that class called SPI in SPI.cpp.
#include "SoftwareSerial.h"
#include "Adafruit_Pixie.h"

//https://forum.arduino.cc/index.php?topic=52111.0
//http://gammon.com.au/forum/?id=10892
//Slave_LED2.ino uses MEGA

#define SS 53

#define NUMPIXELS2 5 // Number of Pixies in the strip

#define PIXIEPIN  6 // Pin number for SoftwareSerial output to the LED chain
SoftwareSerial pixieSerial(-1, PIXIEPIN);
Adafruit_Pixie strip = Adafruit_Pixie(NUMPIXELS2, &pixieSerial);

const int bufferSize = NUMPIXELS2 * 3;
byte buf[bufferSize];
volatile byte m_pos = 0;

volatile boolean process_LEDSignals = false;
 
void setup() {

	pixieSerial.begin(115200); // Pixie REQUIRES this baud rate
 //strip.setBrightness(200);  // Adjust as necessary to avoid blinding

  //Serial.begin(9600);
  // have to send on master in, *slave out*

	SPI.begin();                //PB2 - PB4 are converted to SS/, MOSI, MISO, SCK
	pinMode(SS, INPUT);
	pinMode(MISO, OUTPUT);

	
  // turn on SPI in slave mode
  
  SPCR |= _BV(SPE);
  // SPI통신에서 슬레이브로 동작하도록 설정
  SPCR &= ~_BV(MSTR);
  // SPI  인터럽트 발생을 허용
  SPCR |= _BV(SPIE);
  
  // now turn on interrupts
//  SPI.attachInterrupt();
 // SPI.setClockDivider(SPI_CLOCK_DIV16);
 
  pinMode(PIXIEPIN, OUTPUT);
}
 
 
// SPI interrupt routine
ISR (SPI_STC_vect) {

  byte c = SPDR;  // grab byte from SPI Data Register


  Serial.println(c);

  if( m_pos < sizeof(buf)){
    buf[ m_pos++]=c;
  }

  //check if the "show" command, which tells the LED signals so far arrived to be executed, arrived
  if (m_pos == sizeof(buf))
  //if( c == (byte) 255  )
  {
    process_LEDSignals = true;
	Serial.println("show  com");
  }
}
 
void loop() {

  if(process_LEDSignals){

	 //SPI.beginTransaction(SPISettings(14000000, MSBFIRST, SPI_MODE0)); // disable interrupt

    for(int i=0; i<NUMPIXELS2; i++) { //NUMPIXELS

      strip.setPixelColor (i, buf[i*3+0], buf[i*3+1], buf[i*3+2] );

      Serial.println( buf[i*3+0]);
      Serial.println( buf[i*3+1]);
      Serial.println( buf[i*3+2]);

      }

     strip.show(); // show command should be recieved

    m_pos = 0;
    process_LEDSignals = false; 

	//SPI.endTransaction();
  } // if

  //delay(10);
}
