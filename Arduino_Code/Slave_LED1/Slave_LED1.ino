#include <SPI.h>
#include "SoftwareSerial.h"
#include "Adafruit_Pixie.h"

// Slave_LED1.ino uses UNO
//// 참조 사이트 : https://weathergadget.wordpress.com/2016/05/19/usi-spi-slave-communication/

#define SS 10
#define NUMPIXELS1 7 // Number of Pixies in the strip
#define PIXIEPIN  6 // Pin number for SoftwareSerial output to the LED chain

SoftwareSerial pixieSerial(-1, PIXIEPIN);
Adafruit_Pixie strip = Adafruit_Pixie(NUMPIXELS1, &pixieSerial);

const int bufferSize = NUMPIXELS1 * 3; 
byte showByte = 1; 
byte buf[bufferSize];
volatile byte m_pos = 0;
volatile boolean m_process_it = false;
 
void setup() {
  Serial.begin(9600); // have to send on master in, *slave out*
  pixieSerial.begin(115200); // Pixie REQUIRES this baud rate
  SPI.begin(); //PB2 - PB4 are converted to SS/, MOSI, MISO, SCK

  pinMode(SS, INPUT);
  pinMode(MISO, OUTPUT);
  
  // turn on SPI in slave mode
  SPCR |= bit(SPE);
  // SPI통신 레지스터 설정
  //  SPCR |= _BV(SPE);
    
  // get ready for an interrupt
  m_pos = 0;   // buffer empty
  m_process_it = false;

  //// 슬레이브로 동작하도록 설정
  SPCR &= ~_BV(MSTR);

  ////  인터럽트 발생을 허용
  SPCR |= _BV(SPIE);

  // now turn on interrupts
  //SPI.attachInterrupt();
  SPI.setClockDivider(SPI_CLOCK_DIV16);
  //https://www.arduino.cc/en/Tutorial/SPITransaction

  pinMode(PIXIEPIN, OUTPUT);
}
 
//https://forum.arduino.cc/index.php?topic=52111.0
//It is because they share the pins that we need the SS line.With multiple slaves, 
//only one slave is allowed to "own" the MISO line(by configuring it as an output).So when SS is brought low 
//for that slave it switches its MISO line from high - impedance to output, then it can reply to requests 
//from the master.When the SS is brought high again(inactive) that slave must reconfigure that line as high - impedance, 
//so another slave can use it.

// SPI interrupt routine
ISR (SPI_STC_vect) {
  byte c = SPDR;  // grab byte from SPI Data Register
  
  if( c == 0 ){
    showByte = 0;
    Serial.println("show command");
    }
  else if( m_pos < sizeof(buf)){
    buf[ m_pos++ ]=c;	
  }
  else if( m_pos ==  sizeof(buf) ){
    m_process_it = true;
  }
}
 
void loop() {
  if( m_process_it)
  { 
    //SPI.beginTransaction(SPISettings(14000000, MSBFIRST, SPI_MODE0)); // disable interrupt
    for(int i=0; i<NUMPIXELS1; i++) { //NUMPIXELS
      strip.setPixelColor(i, buf[i*3+0], buf[i*3+1], buf[i*3+2]);
      Serial.println(buf[i*3+0]);
      Serial.print(buf[i*3+1]);
      Serial.print(buf[i*3+2]);
     }
  }
  if(showByte == 0){
    strip.show(); // show command has been  recieved
    showByte = 1;
    m_pos = 0;
    m_process_it = false;
    }
  delay(10);
	//SPI.endTransaction();// // enable interrupt
}
