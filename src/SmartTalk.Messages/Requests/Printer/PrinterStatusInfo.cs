namespace SmartTalk.Messages.Requests.Printer;

public class PrinterStatusInfo
{
        /// <summary>
        /// Operating status of the printer:
        /// If true, then the printer is able to print,
        /// If false then the printer is in state which prevents printing.
        /// </summary>
        public bool Online { get; set; }

        /// <summary>
        /// Returns true if the printer has stopped due to the print head exceeding correct operating temperature
        /// </summary>
        public bool OverTemperature { get; set; }

        /// <summary>
        /// True if the printer has reported that paper is near-end.
        /// </summary>
        public bool PaperLow { get; set; }
        
        /// <summary>
        /// True if the printer has run out of paper entirely
        /// </summary>
        public bool PaperEmpty { get; set; }

        /// <summary>
        /// State of the printer codver, if true then the cover is open.
        /// </summary>
        public bool CoverOpen  { get; set; }

        /// <summary>
        ///  True if the printer board has measured an incorrect voltage, indicating a power supply issue
        /// </summary>
        public bool VoltageError { get; set; }

        /// <summary>
        /// True if paper has jammed in the presenter unit (if the device has a presenter unit fitted)
        /// </summary>
        public bool PresenterPaperJam { get; set; }

        /// <summary>
        ///  True if an autocutter error has occured
        /// </summary>
        public bool CutterError { get; set; }

        /// <summary>
        /// Compulsion switch - returns state of a printer connected cash drawer or other RJ11 connected peripheral.
        /// </summary>
        public bool CompulsionSwitch { get; set; }
        
        /// <summary>
        ///  True if the current error condition is automatically recoverable.
        /// </summary>
        public bool Recoverable { get; set; }
        
        /// <summary>
        /// True if a mechanical, or thermistor error has occured
        /// </summary>
        public bool MechanicalError { get; set; }
        
        /// <summary>
        /// Indicate that the printer is error due to exceeding its data receive buffer without correct handshaking
        /// </summary>
        public bool ReceiveBufferOverflow { get; set; }
        
        /// <summary>
        /// Error when seaching for a registration black mark on paper
        /// </summary>
        public bool BlackMarkError { get; set; }
}